﻿namespace VisualMutator.Model.Mutations
{
    #region

    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using System.Reflection;
    using Exceptions;
    using Extensibility;
    using log4net;
    using Microsoft.Cci;
    using Microsoft.Cci.MutableCodeModel;
    using MutantsTree;
    using Operators;
    using StoringMutants;
    using Strilanc.Value;
    using Types;
    using UsefulTools.CheckboxedTree;
    using UsefulTools.Core;
    using UsefulTools.ExtensionMethods;
    using UsefulTools.Paths;
    using Assembly = Microsoft.Cci.MutableCodeModel.Assembly;

    #endregion

    public interface IMutantsContainer
    {
        void Initialize(ICollection<IMutationOperator> operators, MutantsCreationOptions options, MutationFilter filter);

       
        Mutant CreateEquivalentMutant(out AssemblyNode assemblyNode);

        void SaveMutantsToDisk(MutationTestingSession currentSession);

        MutationResult ExecuteMutation(Mutant mutant, ProgressCounter percentCompleted, CciModuleSource moduleSource);


        IList<AssemblyNode> InitMutantsForOperators(ProgressCounter percentCompleted);
    }

    public class MutantsContainer : IMutantsContainer
    {
        private readonly ICciModuleSource _cci;
        private readonly IWhiteCache _whiteCache;
        private readonly IOperatorUtils _operatorUtils;

        public bool DebugConfig { get; set; }

        private ILog _log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
        private MutantsCreationOptions _options;
        private MutationFilter _filter;
        private ICollection<IMutationOperator> _mutOperators;
        private MultiDictionary<IMutationOperator, MutationTarget> _sharedTargets;

        public MutantsContainer(
            ICciModuleSource cci, 
            IWhiteCache whiteCache,
            IOperatorUtils operatorUtils
            )
        {
            _cci = cci;
            _whiteCache = whiteCache;
            _operatorUtils = operatorUtils;

        }

        public void Initialize(ICollection<IMutationOperator> mutOperators, 
            MutantsCreationOptions options, MutationFilter filter)
        {
            _options = options;
            _filter = filter;
            _mutOperators = mutOperators;
        }

        public Mutant CreateEquivalentMutant(out AssemblyNode assemblyNode)
        {
            var op = new IdentityOperator();
            _sharedTargets = new MultiDictionary<IMutationOperator, MutationTarget>();
            assemblyNode = new AssemblyNode("All modules",null);
            var nsNode = new TypeNamespaceNode(assemblyNode, "");
            assemblyNode.Children.Add(nsNode);
            var typeNode = new TypeNode(nsNode, "");
            nsNode.Children.Add(typeNode);
            var methodNode = new MethodNode(typeNode, "", null, true);
            typeNode.Children.Add(methodNode);
            var group = new MutantGroup("Testing original program", methodNode);
            var target = new MutationTarget(new MutationVariant())
                         {
                             Name = "Original program",
                         };
            var mutant = new Mutant("0", group, target);
           
            group.Children.Add(mutant);
            methodNode.Children.Add(group);
            //assemblyNode.UpdateDisplayedText();
            group.UpdateDisplayedText();
            
            return mutant;
        }

        public void SaveMutantsToDisk(MutationTestingSession currentSession)
        {
            
        }


        public IList<AssemblyNode> InitMutantsForOperators(ProgressCounter percentCompleted)
        {
            var mutantsGroupedByOperators = new List<ExecutedOperator>();
            var root = new MutationRootNode();

            int[] id = { 1 };
            Func<int> genId = () => id[0]++;

            var originalModules = _whiteCache.GetWhiteModules();
            percentCompleted.Initialize(originalModules.Modules.Count);
            var subProgress = percentCompleted.CreateSubprogress();

            var sw = new Stopwatch();

            var assNodes = new List<AssemblyNode>();
            foreach (var module in originalModules.Modules)
            {

                sw.Restart();

                AssemblyNode assemblyNode = FindTargets(module);
                assNodes.Add(assemblyNode);
                percentCompleted.Progress();
            }


            root.State = MutantResultState.Untested;

            return assNodes;
        }

        private IList<MutationTarget> LimitMutationTargets(IEnumerable<MutationTarget> targets)
        {
           // return targets.ToList();
            var mapping = targets//.Shuffle()
              //  .SelectMany(pair => pair.Item2.Select(t => Tuple.Create(pair.Item1, t))).Shuffle()
                .Take(_options.MaxNumerOfMutantPerOperator).ToList();
            return mapping;
        }


        public AssemblyNode FindTargets(IModuleInfo module)
        {
            _log.Info("Finding targets for module: " + module.Name);
            _log.Info("Using mutation operators: " + _mutOperators.Select(_=>_.Info.Id)
                .MayAggregate((a,b)=>a+","+b).Else("None"));

            var mergedTargets = new MultiDictionary<IMutationOperator, MutationTarget>();
            _sharedTargets = new MultiDictionary<IMutationOperator, MutationTarget>();
            foreach (var mutationOperator in _mutOperators)
            {
                try
                {
                    var ded = mutationOperator.CreateVisitor();
                    IOperatorCodeVisitor operatorVisitor = ded;
                    operatorVisitor.Host = _cci.Host;
                    operatorVisitor.OperatorUtils = _operatorUtils;
                    operatorVisitor.Initialize();

                    var visitor = new VisualCodeVisitor(mutationOperator.Info.Id, operatorVisitor, module.Module);

                    var traverser = new VisualCodeTraverser(_filter, visitor);

                    traverser.Traverse(module.Module);
                    visitor.PostProcess();

                    IEnumerable<MutationTarget> mutations = LimitMutationTargets(visitor.MutationTargets);


                    mergedTargets.Add(mutationOperator, new HashSet<MutationTarget>(mutations));
                    _sharedTargets.Add(mutationOperator, new HashSet<MutationTarget>(visitor.SharedTargets));
                    
                }
                catch (Exception e)
                {
                    if (!DebugConfig)
                    {
                        throw new MutationException("Finding targets operation failed in operator: {0}.".Formatted(mutationOperator.Info.Name), e);
                    }
                    else
                    {
                        throw;
                    }
                }
            }
            var assemblyNode = BuildMutantsTree(module, mergedTargets);
                
            _log.Info("Found total of: " + mergedTargets.Values.Count() + " mutation targets in "+assemblyNode.Name);
            return assemblyNode;

            
            
        }

        private AssemblyNode BuildMutantsTree(IModuleInfo module, 
            MultiDictionary<IMutationOperator, MutationTarget> mutationTargets)
        {

            var assemblyNode = new AssemblyNode(module.Name, module);

            System.Action<CheckedNode, ICollection<MutationTarget>> typeNodeCreator = (parent, targets) =>
                {
                    foreach (var byTypeGrouping in targets
                        .OrderBy(t => t.TypeName)
                        .GroupBy(t => t.TypeName))
                    {
                        var type = new TypeNode(parent, byTypeGrouping.Key);
                        parent.Children.Add(type);

                        var groupedByMethodTargets = byTypeGrouping.GroupBy(target => target.MethodRaw.Name.Value);
                        foreach (var byMethodGrouping in groupedByMethodTargets.OrderBy(g => g.Key))
                        {
                            var md = byMethodGrouping.First().MethodRaw;
                            var method = new MethodNode(type, md.Name.Value, md, true);
                            type.Children.Add(method);

                            var groupedTargets = byMethodGrouping.GroupBy(target => target.GroupName);
                            foreach (var byGroupGrouping in groupedTargets.OrderBy(g => g.Key))
                            {
                                var group = new MutantGroup(byGroupGrouping.Key, method);
                                method.Children.Add(group);
                                foreach (var mutationTarget in byGroupGrouping)
                                {
                                    var mutant = new Mutant(mutationTarget.Id, group, mutationTarget);
                                    group.Children.Add(mutant);

                                }
                                group.UpdateDisplayedText();
                            }
                        }
                    }
                };

            Func<MutationTarget, string> namespaceExtractor = target => target.NamespaceName;

            NamespaceGrouper<MutationTarget, CheckedNode>.GroupTypes(assemblyNode, 
                namespaceExtractor, 
                (parent, name) => new TypeNamespaceNode(parent, name), 
                typeNodeCreator,
                mutationTargets.Values.SelectMany(a => a).ToList());
          

            return assemblyNode;


        }

        public MutationResult ExecuteMutation(Mutant mutant,  ProgressCounter percentCompleted, 
            CciModuleSource moduleSource)
        {
            _log.Debug("ExecuteMutation in object: " +ToString()+ GetHashCode());
            IMutationOperator mutationOperator = mutant.MutationTarget.OperatorId == null? new IdentityOperator() : 
                _mutOperators.Single(m => mutant.MutationTarget.OperatorId == m.Info.Id);
                
            try
            {
                _log.Info("Execute mutation of " + mutant.MutationTarget + " contained in " + mutant.MutationTarget.MethodRaw + " modules. " );
                var mutatedModules = new List<IModuleInfo>();
                foreach (var module in moduleSource.Modules)
                {
                    percentCompleted.Progress();
                    var visitorBack = new VisualCodeVisitorBack(mutant.MutationTarget.InList(),
                         _sharedTargets.GetValues(mutationOperator, returnEmptySet: true), 
                         module.Module, mutationOperator.Info.Id);
                    var traverser2 = new VisualCodeTraverser(_filter, visitorBack);
                    traverser2.Traverse(module.Module);
                    visitorBack.PostProcess();
                    var operatorCodeRewriter = mutationOperator.CreateRewriter();

                    var rewriter = new VisualCodeRewriter(_cci.Host, visitorBack.TargetAstObjects,
                        visitorBack.SharedAstObjects, _filter, operatorCodeRewriter);

                    operatorCodeRewriter.MutationTarget =
                        new UserMutationTarget(mutant.MutationTarget.Variant.Signature, mutant.MutationTarget.Variant.AstObjects);
                    
                    operatorCodeRewriter.NameTable = _cci.Host.NameTable;
                    operatorCodeRewriter.Host = _cci.Host;
                    operatorCodeRewriter.Module = module.Module;
                    operatorCodeRewriter.OperatorUtils = _operatorUtils;
                    operatorCodeRewriter.Initialize();

                    var rewrittenModule = (Assembly) rewriter.Rewrite(module.Module);

                    rewriter.CheckForUnfoundObjects();
                    mutatedModules.Add(new ModuleInfo(rewrittenModule, ""));
                }
               // moduleSource.Merge(mutatedModules);
              ////  return moduleSource;
                return new MutationResult(new SimpleModuleSource(mutatedModules.ToList()), moduleSource);
            }
            catch (Exception e)
            {
                if (!DebugConfig)
                {
                    throw new MutationException("CreateMutants failed on operator: {0}.".Formatted(mutationOperator.Info.Name), e);
                }
                else
                {
                    throw;
                }
                
            }
        }
        public class OperatorWithTargets
        {
            public List<MutationTarget> MutationTargets
            {
                get;
                set;
            }
            public IMutationOperator Operator
            {
                get;
                set;
            }
            public List<MutationTarget> CommonTargets
            {
                get;
                set;
            }

        }
    }
    public class MutationResult
    {
        public MutationResult(IModuleSource mutatedModules, ICciModuleSource whiteModules)
        {
            MutatedModules = mutatedModules;
            WhiteModules = whiteModules;
        }

        public ICciModuleSource WhiteModules { get; private set; }
        public IModuleSource MutatedModules { get; private set; }
    }
}