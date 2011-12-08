﻿namespace VisualMutator.ViewModels
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Linq;
    using System.Text;

    using CommonUtilityInfrastructure.WpfUtils;

    using VisualMutator.Model.Mutations.Operators;
    using VisualMutator.Model.Mutations.Types;
    using VisualMutator.Views;

    public class SessionCreationViewModel : CreationViewModel<ISessionCreationView>
    {
   


        private MutantsTestingOptionsViewModel _mutantsTesting;

        public SessionCreationViewModel(
            ISessionCreationView view,
            TypesTreeViewModel typesTree, 
            MutationsTreeViewModel mutationsTree, 
            MutantsCreationOptionsViewModel mutantsCreation,
             MutantsTestingOptionsViewModel mutantsTesting)
            : base(view, typesTree, mutationsTree, mutantsCreation)
        {
            MutantsTesting = mutantsTesting;
        }


        public MutantsTestingOptionsViewModel MutantsTesting
        {
            get
            {
                return _mutantsTesting;
            }
            set
            {
                SetAndRise(ref _mutantsTesting, value, () => MutantsTesting);
            }
        }





    }
}