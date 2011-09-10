﻿namespace PiotrTrzpil.VisualMutator_VSPackage.Infrastructure.NinjectModules
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;

    using Ninject;
    using Ninject.Activation;
    using Ninject.Modules;

    using PiotrTrzpil.VisualMutator_VSPackage.Controllers;
    using PiotrTrzpil.VisualMutator_VSPackage.Model.Tests;
    using PiotrTrzpil.VisualMutator_VSPackage.ViewModels;
    using PiotrTrzpil.VisualMutator_VSPackage.Views;
    using PiotrTrzpil.VisualMutator_VSPackage.Views.Abstract;

    public class UnitTestsModule : NinjectModule 
    {
        public override void Load()
        {
            Kernel.Bind<UnitTestsController>().ToSelf().InSingletonScope();
            Kernel.Bind<UnitTestsViewModel>().ToSelf().InSingletonScope();
            Kernel.Bind<IUnitTestsView>().To<UnitTestsView>().InSingletonScope();
            


            Kernel.Bind<ITestsContainer>().To<TestsContainer>().InSingletonScope();



            Kernel.Bind<NUnitTestService>().ToSelf().InSingletonScope();
            Kernel.Bind<MsTestService>().ToSelf().InSingletonScope();

            Kernel.Bind<INUnitWrapper>().To<NUnitWrapper>().InSingletonScope();
            Kernel.Bind<IMsTestWrapper>().To<MsTestWrapper>().InSingletonScope();
            


            


            Kernel.Bind<IEnumerable<ITestService>>().ToConstant(CreateTestService(Kernel));



        }

        private IEnumerable<ITestService> CreateTestService(IKernel kernel)
        {
            return new ITestService[]
            {
                kernel.Get<NUnitTestService>(),
                kernel.Get<MsTestService>()
            };
      
        }


    }
}