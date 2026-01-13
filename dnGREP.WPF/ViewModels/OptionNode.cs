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
using Newtonsoft.Json.Linq;
using NLog;
using Windows.Win32.UI.Input.KeyboardAndMouse;
using static System.Net.Mime.MediaTypeNames;
using Resources = dnGREP.Localization.Properties.Resources;

namespace dnGREP.WPF
{
    public enum OptionNodeType { Root, Static, Checkbox, ComboBox }

    public interface IOptionNode
    {
        IOptionNode AddChild(IOptionNode child);

        IOptionNode? Parent { get; set; }

        int Level { get; set; }

        OptionNodeType NodeType { get; }
        string Text { get; }
        string PreText { get; }
        string PostText { get; }
        string? Tooltip { get; }
        string SettingsKey { get; }

        void SaveSetting();
    }

    public partial class StaticOptionNode : CultureAwareViewModel, IOptionNode
    {
        public static StaticOptionNode GlobalRoot = new(
            string.Empty, string.Empty, string.Empty, null);

        public StaticOptionNode(
            string text, string preText, string postText, string? tooltip)
        {
            NodeType = OptionNodeType.Static;
            Text = text;
            PreText = preText;
            PostText = postText;
            Tooltip = tooltip;
        }

        public IOptionNode AddChild(IOptionNode child) 
        {
            child.Parent = this;
            child.Level = Level + 1;

            childNodes.Add(child);

            return child;
        }

        public ObservableCollection<IOptionNode> childNodes = [];

        [ObservableProperty]
        private IOptionNode? parent;

        [ObservableProperty]
        private int level = 0;

        [ObservableProperty]
        private OptionNodeType nodeType = OptionNodeType.Static;

        [ObservableProperty]
        private string text = string.Empty;

        [ObservableProperty]
        private string preText = string.Empty;

        [ObservableProperty]
        private string postText = string.Empty;

        [ObservableProperty]
        private string? tooltip;

        public string SettingsKey => string.Empty;

        public virtual void SaveSetting() { }
    }


    public partial class OptionNode<T> : StaticOptionNode
    {
        public OptionNode(OptionNodeType nodeType, 
            string text, string preText, string postText, string? tooltip,
            string settingsKey)
            : base(text, preText, postText, tooltip)
        {
            NodeType = nodeType;
            SettingsKey = settingsKey;
            Value = GrepSettings.Instance.Get<T>(SettingsKey);
        }

        public override void SaveSetting()
        {
            GrepSettings.Instance.Set(SettingsKey, Value);
        }


        [ObservableProperty]
        private T? value = default;

        [ObservableProperty]
        private string settingsKey = string.Empty;
    }
}
