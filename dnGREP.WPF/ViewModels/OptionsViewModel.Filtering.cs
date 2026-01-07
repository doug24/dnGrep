using System;
using System.Collections;
using System.Collections.Generic;
using System.Resources;
using CommunityToolkit.Mvvm.ComponentModel;
using dnGREP.Common;
using dnGREP.Localization;

namespace dnGREP.WPF
{
    public partial class OptionsViewModel
    {
        private TreeNode<VisibilityData> filterTree = new(new("root", true), null);
        private readonly List<string> uiStrings = [];

        private void BuildVisibilityTree()
        {
            ResourceSet? resources = dnGREP.Localization.Properties.Resources.ResourceManager.GetResourceSet(TranslationSource.Instance.CurrentCulture, false, true);
            if (resources != null)
            {
                foreach (var obj in resources)
                {
                    if (obj is DictionaryEntry entry &&
                        entry.Key is string key &&
                        key.StartsWith("Options_", StringComparison.OrdinalIgnoreCase) &&
                        entry.Value is string value)
                    {
                        uiStrings.Add(value);
                    }
                }
            }
        }

        [ObservableProperty]
        private bool startupOptionsVisibility;

        [ObservableProperty]
        private bool showDnGrepInExplorerRightClickMenuVisibility;
    }

    public record VisibilityData(string Name, bool Visible);
}
