namespace VisualMutator.Extensibility
{
    using System.Collections.Generic;

    using Mono.Cecil;

    public class MutationElementType : MonoCecilTypeIdentifier, IMutationElement<TypeDefinition>
    {
        public MutationElementType(TypeDefinition type)
            : base(type)
        {
          
        }

        public TypeDefinition FindIn(ICollection<AssemblyDefinition> assemblies)
        {
            return this.FindType(assemblies);
        }
    }
}