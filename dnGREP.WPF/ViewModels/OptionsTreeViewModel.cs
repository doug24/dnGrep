using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using dnGREP.Common;
using dnGREP.Common.UI;
using dnGREP.Engines;
using dnGREP.Localization;
using dnGREP.WPF.MVHelpers;
using Microsoft.Win32;
using NLog;
using Windows.Win32.UI.Input.KeyboardAndMouse;
using Resources = dnGREP.Localization.Properties.Resources;

namespace dnGREP.WPF
{
    public partial class OptionsTreeViewModel : CultureAwareViewModel
    {
        public OptionsTreeViewModel()
        {
            BuildTree();
        }

        private void BuildTree()
        {
            var startupRoot = new StaticOptionNode(Resources.Options_StartupOptions, 
                string.Empty, string.Empty, null);

            startupRoot.AddChild(new StaticOptionNode(Resources.Options_ShowDnGrepInExplorerRightClickMenu,
                string.Empty, string.Empty, null));

            Options.Add(startupRoot);

        }

        public ObservableCollection<IOptionNode> Options { get; } = [];
    }
}
