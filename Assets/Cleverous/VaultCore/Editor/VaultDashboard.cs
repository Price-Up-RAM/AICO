// (c) Copyright Cleverous 2022. All rights reserved.

using System;
using System.Diagnostics;
using Cleverous.VaultSystem;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using Debug = UnityEngine.Debug;
using Object = UnityEngine.Object;
using Random = UnityEngine.Random;

namespace Cleverous.VaultDashboard
{
    public class VaultDashboard : EditorWindow
    {
        // current values
        private const string UxmlAssetName = "vault_dashboard_uxml";

        public static DataEntity CurrentSelectedAsset
        {
            get
            {
                if (Instance.m_currentSelectedAsset != null)
                {
                    return Instance.m_currentSelectedAsset;
                }

                string currentGuid = VaultEditorSettings.GetString(VaultEditorSettings.VaultData.CurrentAssetGuid);
                string currentPath = AssetDatabase.GUIDToAssetPath(currentGuid);
                DataEntity asset = AssetDatabase.LoadAssetAtPath<DataEntity>(currentPath);
                Instance.m_currentSelectedAsset = asset;
                return Instance.m_currentSelectedAsset;
            }
            private set
            {
                string currentPath = AssetDatabase.GetAssetPath(value);
                GUID currentGuid = AssetDatabase.GUIDFromAssetPath(currentPath);
                VaultEditorSettings.SetString(VaultEditorSettings.VaultData.CurrentAssetGuid, currentGuid.ToString());
                Instance.m_currentSelectedAsset = value;
            }
        }
        private DataEntity m_currentSelectedAsset;

        public static IDataGroup CurrentSelectedGroup
        {
            get
            {
                if (Instance.m_currentGroupSelected != null)
                {
                    return Instance.m_currentGroupSelected;
                }

                string currentName = VaultEditorSettings.GetString(VaultEditorSettings.VaultData.CurrentGroupName);
                VaultDataGroupFoldableButton button = GroupColumn.Q<VaultDataGroupFoldableButton>(currentName);
                if (button == null)
                {
                    // Debug.Log($"Failed to find a group button '{currentName}'.");
                    return null;
                }

                IDataGroup asset = button.DataGroup;
                if (asset == null)
                {
                    Debug.Log($"Failed to find group asset '{currentName}'.");
                }
                Instance.m_currentGroupSelected = asset;
                return Instance.m_currentGroupSelected;
            }
            private set
            {
                string title = value == null
                    ? "NULL GROUP"
                    : value.Title;
                VaultEditorSettings.SetString(VaultEditorSettings.VaultData.CurrentGroupName, title);
                Instance.m_currentGroupSelected = value;
            }
        }
        private IDataGroup m_currentGroupSelected;

        // toolbar
        protected static Historizer Historizer;
        public static ToolbarSearchField SearchFieldForGroup; // TODO move these.
        public static ToolbarSearchField SearchFieldForAsset;// TODO move these.
        public static bool SearchTypeIsDirty => SearchFieldForGroup != null && SearchFieldForGroup.value != m_typeSearchCache;
        public static bool SearchAssetIsDirty => SearchFieldForAsset != null && SearchFieldForAsset.value != m_assetSearchCache;
        private static string m_assetSearchCache;
        private static string m_typeSearchCache;

        // columns
        public static VaultDataGroupColumn GroupColumn;
        public static VaultColumnOfAssets AssetColumn;
        public static VaultAssetInspector InspectorColumn;

        /*

        // action callbacks ////////////////

        // major changers
        public static Action OnCurrentAssetChanged;
        public static Action OnCurrentGroupChanged;

        // searches
        public static Action OnSearchAssets;
        public static Action OnSearchGroups;

        // assets
        public static Action OnDeleteAssetStart;
        public static Action OnDeleteAssetComplete;
        public static Action OnCreateNewAssetStart;
        public static Action OnCreateNewAssetComplete;
        public static Action OnCloneAssetStart;
        public static Action OnCloneAssetComplete;

        // groups
        public static Action OnCreateGroupStart;
        public static Action OnCreateGroupComplete;
        */

        // wrappers for views
        protected static VisualElement WrapperForGroupContent;
        protected static VisualElement WrapperForAssetList;
        protected static VisualElement WrapperForAssetContent;
        protected static VisualElement WrapperForInspector;

        private static ToolbarButton m_assetNewButton;
        private static ToolbarButton m_assetDeleteButton;
        private static ToolbarButton m_assetCloneButton;
        private static ToolbarButton m_assetRemoveFromGroupButton;
        private static ToolbarButton m_groupNewButton;
        private static ToolbarButton m_refreshButton;
        private static ToolbarButton m_helpButton;
        private static ToolbarButton m_groupDelButton;
        private static Button m_idSetButton;
        private static IntegerField m_idSetField;
        
        public static VaultDashboard Instance;

        private static readonly StyleColor ButtonInactive = new StyleColor(Color.gray);
        private static readonly StyleColor ButtonActive = new StyleColor(Color.white);
        private static bool m_idValueIsDirty = true;

        [MenuItem("Tools/Cleverous/Vault Dashboard %#d", priority = 0)]
        public static void Open()
        {
            //Debug.Log("Open()");
            if (Instance != null)
            {
                FocusWindowIfItsOpen(typeof(VaultDashboard));
                return;
            }

            Instance = GetWindow<VaultDashboard>();
            Instance.titleContent.text = "Vault Dashboard";
            Instance.minSize = new Vector2(850, 200);
            Instance.Show();
            Instance.RebuildFull(); 
        }
        private void OnEnable()
        { 
            Instance = this; 
            AssemblyReloadEvents.afterAssemblyReload += DatabaseBuilder.CallbackAfterScriptReload;
        }
        private void OnDisable()
        {
            AssemblyReloadEvents.afterAssemblyReload -= DatabaseBuilder.CallbackAfterScriptReload;
        }
        public void Update()
        {
            if (SearchAssetIsDirty)
            {
                m_assetSearchCache = SearchFieldForAsset.value;
                VaultEditorSettings.SetString(VaultEditorSettings.VaultData.SearchAssets, m_assetSearchCache);
                AssetColumn.ListAssetsBySearch();
            }

            if (SearchTypeIsDirty)
            {
                m_typeSearchCache = SearchFieldForGroup.value;
                VaultEditorSettings.SetString(VaultEditorSettings.VaultData.SearchGroups, m_typeSearchCache);
                GroupColumn.Filter(SearchFieldForGroup.value);
            }

            if (m_idValueIsDirty)
            {
                SetIdStartingPoint(VaultEditorSettings.GetInt(VaultEditorSettings.VaultData.StartingKeyId));
                m_idValueIsDirty = false;
            }
        }
        
        [InitializeOnLoadMethod]
        private static void OnRecompile()
        {
            m_idValueIsDirty = true;
        }
        private void LoadUxmlTemplate()
        {
            Instance.rootVisualElement.Clear();


            // load uxml and elements
            VisualTreeAsset visualTree = Resources.Load<VisualTreeAsset>(UxmlAssetName);
            visualTree.CloneTree(rootVisualElement);


            // find important parts and reference them
            WrapperForGroupContent = rootVisualElement.Q<VisualElement>("GC_CONTENT");
            WrapperForAssetContent = rootVisualElement.Q<VisualElement>("AC_CONTENT");
            WrapperForAssetList = rootVisualElement.Q<VisualElement>("ASSET_COLUMN");
            WrapperForInspector = rootVisualElement.Q<VisualElement>("INSPECT_COLUMN");
            SearchFieldForGroup = rootVisualElement.Q<ToolbarSearchField>("GROUP_SEARCH");
            SearchFieldForAsset = rootVisualElement.Q<ToolbarSearchField>("ASSET_SEARCH");

            Historizer = new Historizer(); 
            rootVisualElement.Q<VisualElement>("TB_HISTORY").Add(Historizer);


            // init group column buttons
            m_groupNewButton = rootVisualElement.Q<ToolbarButton>("GC_NEW");
            m_groupNewButton.style.backgroundImage = new StyleBackground(VaultEditorUtility.GetEditorImage("cyl_add"));
            m_groupNewButton.clicked += CreateNewDataGroupCallback;

            m_groupDelButton = rootVisualElement.Q<ToolbarButton>("GC_DEL");
            m_groupDelButton.style.backgroundImage = new StyleBackground(VaultEditorUtility.GetEditorImage("cyl_del"));
            m_groupDelButton.clicked += DeleteSelectedDataGroup;

            m_refreshButton = rootVisualElement.Q<ToolbarButton>("GC_RELOAD");
            m_refreshButton.style.backgroundImage = new StyleBackground(VaultEditorUtility.GetEditorImage("refresh"));
            m_refreshButton.clicked += CallbackButtonRefresh;

            m_helpButton = rootVisualElement.Q<ToolbarButton>("GC_HELP");
            m_helpButton.style.backgroundImage = new StyleBackground(VaultEditorUtility.GetEditorImage("help"));
            m_helpButton.clicked += CallbackButtonHelp;


            // init Asset Column Buttons
            m_assetNewButton = WrapperForAssetList.Q<ToolbarButton>("AC_NEW");
            m_assetNewButton.style.backgroundImage = new StyleBackground(VaultEditorUtility.GetEditorImage("cube_new"));
            m_assetNewButton.clicked += CreateNewAssetCallback;

            m_assetDeleteButton = WrapperForAssetList.Q<ToolbarButton>("AC_DELETE");
            m_assetDeleteButton.style.backgroundImage = new StyleBackground(VaultEditorUtility.GetEditorImage("cube_del"));
            m_assetDeleteButton.clicked += DeleteSelectedAsset;

            m_assetCloneButton = WrapperForAssetList.Q<ToolbarButton>("AC_CLONE");
            m_assetCloneButton.style.backgroundImage = new StyleBackground(VaultEditorUtility.GetEditorImage("clone"));
            m_assetCloneButton.clicked += CloneSelectedAsset;

            m_assetRemoveFromGroupButton = WrapperForAssetList.Q<ToolbarButton>("AC_GROUP_REMOVE");
            m_assetRemoveFromGroupButton.style.backgroundImage = new StyleBackground(VaultEditorUtility.GetEditorImage("cyl_sub"));
            m_assetRemoveFromGroupButton.clicked += RemoveAssetFromGroup;


            // init footer
            m_idSetButton = rootVisualElement.Q<Button>("ID_SET_BUTTON");
            m_idSetButton.clicked += SetIdCallback;

            m_idSetField = rootVisualElement.Q<IntegerField>("ID_SET_FIELD");
            m_idSetField.SetValueWithoutNotify(VaultEditorSettings.GetInt(VaultEditorSettings.VaultData.StartingKeyId));

            WrapperForGroupContent.Add(GroupColumn);
            WrapperForAssetContent.Add(AssetColumn);
            WrapperForInspector.Add(InspectorColumn);


            // init split pane draggers
            // BUG - basically we have to do this because there is no proper/defined initialization for the drag anchor position.

            SplitView mainSplit = rootVisualElement.Q<SplitView>("MAIN_SPLIT");
            mainSplit.fixedPaneInitialDimension = 549;

            SplitView columnSplit = rootVisualElement.Q<SplitView>("FILTERS_PICK_SPLIT");
            columnSplit.fixedPaneInitialDimension = 250;

            SetIdStartingPoint(VaultEditorSettings.GetInt(VaultEditorSettings.VaultData.StartingKeyId));
        }
        public void RebuildFull()
        {
            // Debug.Log($"Start RebuildFull() - Dashboard is {(Instance == null ? "null" : "valid")}");
            if (Instance == null) return;
            Stopwatch sw = new Stopwatch();
            sw.Start();
            Instance.LoadUxmlTemplate();
            Rebuild(true);
            sw.Stop();
            // Debug.Log($"<color=orange>VaultDashboard.RebuildFull(): {sw.Elapsed.Milliseconds}ms</color>");
        }
        public void Rebuild(bool fullRebuild = false)
        {
            //Debug.Log($"... Rebuild()");
            // search data
            SearchFieldForGroup.SetValueWithoutNotify(VaultEditorSettings.GetString(VaultEditorSettings.VaultData.SearchGroups));
            SearchFieldForAsset.SetValueWithoutNotify(VaultEditorSettings.GetString(VaultEditorSettings.VaultData.SearchAssets));
            m_typeSearchCache = SearchFieldForGroup.value;
            m_assetSearchCache = SearchFieldForAsset.value;

            // rebuild
            RebuildGroupColumn(fullRebuild);
            RebuildInspectorColumn(fullRebuild);
            RebuildAssetColumn(fullRebuild);
            SetCurrentGroup(CurrentSelectedGroup);
        }

        private void RebuildGroupColumn(bool fullRebuild = false)
        {
            if (fullRebuild || GroupColumn == null)
            {
                WrapperForGroupContent.Clear();
                GroupColumn = new VaultFilterColumnInheritance();
                WrapperForGroupContent.Add(GroupColumn);
            }
            GroupColumn.VaultPanelReload();
        }
        private void RebuildAssetColumn(bool fullRebuild = false)
        {
            if (fullRebuild || AssetColumn == null)
            {
                WrapperForAssetContent.Clear();
                AssetColumn = new VaultColumnOfAssets();
                WrapperForAssetContent.Add(AssetColumn);
            }
            AssetColumn.VaultPanelReload();
        }
        private void RebuildInspectorColumn(bool fullRebuild = false)
        {
            if (fullRebuild || InspectorColumn == null)
            {
                InspectorColumn?.RemoveFromHierarchy();
                InspectorColumn = new VaultAssetInspector();
                WrapperForInspector.Add(InspectorColumn);
            }
            InspectorColumn.VaultPanelReload();
        }

        public static void SetCurrentGroup(IDataGroup group)
        {
            if (group == null) return;

            bool isCustom = group.GetType() == typeof(VaultCustomDataGroup);
            m_assetRemoveFromGroupButton.SetEnabled(isCustom);
            m_assetRemoveFromGroupButton.style.unityBackgroundImageTintColor = isCustom ? ButtonActive : ButtonInactive;

            CurrentSelectedGroup = group;
            GroupColumn.SelectButtonByTitle(group.Title);
            AssetColumn.ListAssetsByGroup(true);
            SearchFieldForAsset.value = string.Empty;
        }
        public static void SetCurrentInspectorAsset(DataEntity asset)
        {
            CurrentSelectedAsset = asset;
            InspectorColumn.VaultPanelReload();
            Historizer.AddAndHistorize();
        }
        public static void InspectAssetRemote(Object asset, Type t)
        {
            if (asset == null && t == null) return;
            if (t == null) return;

            if (Instance == null) Open();
            Instance.Focus();
            SearchFieldForAsset.SetValueWithoutNotify(string.Empty);

            VisualElement button = WrapperForGroupContent.Q<VisualElement>(t.Name);
            IVaultDataGroupButton buttonInterface = (IVaultDataGroupButton) button;
            if (buttonInterface != null)
            {
                buttonInterface.SetAsCurrent();
                GroupColumn.ScrollTo(button);
            }
            
            if (asset != null) AssetColumn.Pick((DataEntity)asset);
            InspectorColumn.VaultPanelReload();
        }

        /// <summary>
        /// The Dashboard button calls this to create a new asset in the current group.
        /// </summary>
        private static void CreateNewAssetCallback()
        {
            if (CurrentSelectedGroup.SourceType.IsAbstract)
            {
                bool confirm = EditorUtility.DisplayDialog(
                    "Group Error",
                    "Selected Class is abstract! We can't create a new asset in abstract class groups. Choose a valid class and create a new Data Asset, then you can store it in a Custom Group.",
                    "Ok");
                if (confirm) return;
            }
            CreateNewAsset();
        }
        /// <summary>
        /// Create a new asset with the current group Type.
        /// </summary>
        /// <returns></returns>
        public static void CreateNewAsset()
        {
            AssetColumn.NewAsset(CurrentSelectedGroup.SourceType);
        }
        /// <summary>
        /// Create a new asset with a specific Type.
        /// </summary>
        /// <param name="t"></param>
        /// <returns></returns>
        public static DataEntity CreateNewAsset(Type t)
        {
            Debug.Log($"Create new asset with specific Type: {t.Name}");
            DataEntity newAsset = AssetColumn.NewAsset(t);
            DatabaseBuilder.Reload();
            Instance.RebuildFull();
            return newAsset;
        }
        public static void CloneSelectedAsset()
        {
            AssetColumn.CloneSelection();
        }
        public static void DeleteSelectedAsset()
        {
            AssetColumn.DeleteSelection();
        }

        public static void SetIdStartingPoint(int id)
        {
            Vault.Db.SetIdStartingValue(id);
            VaultEditorSettings.SetInt(VaultEditorSettings.VaultData.StartingKeyId, id);
            if (m_idSetField != null) m_idSetField.value = id;
            if (Vault.Db != null) EditorUtility.SetDirty(Vault.Db);
        }
        private static void SetIdCallback()
        {
            SetIdStartingPoint(m_idSetField.value);
        }

        public static void RemoveAssetFromGroup()
        {
            CurrentSelectedGroup.RemoveEntity(CurrentSelectedAsset.GetDbKey());
            AssetColumn.VaultPanelReload();
        }
        public static void CreateNewDataGroupCallback()
        {
            CreateNewDataGroup();
        }
        public static void DeleteSelectedDataGroup()
        {
            if (CurrentSelectedGroup == null) return;
            if (CurrentSelectedGroup.GetType() != typeof(VaultCustomDataGroup)) return;
            VaultCustomDataGroup customGroup = (VaultCustomDataGroup) CurrentSelectedGroup;
            if (customGroup == null) return;

            bool confirm = EditorUtility.DisplayDialog(
                "Delete Custom Group",
                $"Are you sure you want to permanently delete '{CurrentSelectedGroup.Title}'?",
                "Delete",
                "Abort");
            if (!confirm) return;

            InspectAssetRemote(null, typeof(object));
            AssetDatabase.DeleteAsset(AssetDatabase.GetAssetPath(customGroup));
            CurrentSelectedGroup = null;
            Instance.Rebuild();
        }
        public static VaultCustomDataGroup CreateNewDataGroup()
        {
            VaultCustomDataGroup result = (VaultCustomDataGroup)AssetColumn.NewAsset(typeof(VaultCustomDataGroup));
            GroupColumn.VaultPanelReload();
            InspectAssetRemote(result, typeof(VaultCustomDataGroup));
            return null;
        }
        public void CallbackButtonRefresh()
        {
            RebuildFull();
        }
        public static void CallbackButtonHelp()
        {
            Application.OpenURL("https://lanefox.gitbook.io/vault/");
        }
    }
}