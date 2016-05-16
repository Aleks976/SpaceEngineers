#region Using

using ParallelTasks;
using Sandbox.Common.ObjectBuilders;
using Sandbox.Common.ObjectBuilders.Definitions;
using Sandbox.Definitions;
using Sandbox.Engine.Networking;
using Sandbox.Game.Entities;
using Sandbox.Game.Entities.Cube;
using Sandbox.Game.Localization;
using Sandbox.Game.Multiplayer;
using Sandbox.Game.World;
using Sandbox.Graphics;
using Sandbox.Graphics.GUI;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;

using VRage;
using VRage;
using VRage.Input;
using VRage.Utils;
using VRage.Compression;
using VRageMath;
using VRage.Library.Utils;
using VRage.FileSystem;
using Sandbox.Engine.Utils;
using VRage.ObjectBuilders;
using VRage.Collections;
using VRage.Game;
using VRage.Network;
using Sandbox.Engine.Multiplayer;
using VRage.Game.Entity;
using Sandbox.Common;
using Sandbox.Game.Entities.Character;
using Sandbox.ModAPI;

#endregion


namespace Sandbox.Game.Gui
{

    [StaticEventOwner]
    public class MyGuiScreenVehicleEditorSaveLoad : MyGuiBlueprintScreenBase
    {
        public static Task Task;
        private static bool m_downloadFromSteam = true;
        private static readonly Vector2 SCREEN_SIZE = new Vector2(0.40f, 1.2f);
        private static readonly float HIDDEN_PART_RIGHT = 0.04f;
        private static List<MyGuiControlListbox.Item> m_recievedBlueprints = new List<MyGuiControlListbox.Item>();
        private static bool m_needsExtract = false;
        public static List<MySteamWorkshop.SubscribedItem> m_subscribedItemsList = new List<MySteamWorkshop.SubscribedItem>();

        private Vector2 m_controlPadding = new Vector2(0.02f, 0.02f);
        private float m_textScale = 0.8f;

        private MyGuiControlButton m_detailsButton;
        private MyGuiControlButton m_screenshotButton;
        //private MyGuiControlButton m_replaceButton;
        private MyGuiControlButton m_deleteButton;
        private MyGuiControlButton m_okButton;

        //private MyGuiControlTextbox m_searchBox;
        //private MyGuiControlButton m_searchClear;
        private static MyGuiControlListbox m_blueprintList = new MyGuiControlListbox(visualStyle: MyGuiControlListboxStyleEnum.Blueprints);
        private MyGuiDetailScreenBase m_detailScreen = null;
        private MyGuiControlImageButton m_thumbnailImage;
        private MyGuiControlImageButton m_selectedImage;
        private bool m_activeDetail = false;
        private MyGuiControlListbox.Item m_selectedItem = null;
        private MyGuiControlRotatingWheel m_wheel;
        private MyGridClipboard m_clipboard;
        private Boolean m_gridDeleted = false;

        static HashSet<ulong> m_downloadQueued = new HashSet<ulong>();
        static MyConcurrentHashSet<ulong> m_downloadFinished = new MyConcurrentHashSet<ulong>();

        static string TEMP_PATH = Path.Combine(m_airplaneWorkshopBlueprintFolder, "temp");

        private string[] filenames;

        private Vector3D positionBeforeScreen = new Vector3D();

        static MyGuiScreenVehicleEditorSaveLoad()
        {
        }

        public override string GetFriendlyName()
        {
            return "MyBlueprintScreen";
        }

        public MyGuiScreenVehicleEditorSaveLoad(bool exitingGame = false) :
            base(new Vector2(MyGuiManager.GetMaxMouseCoord().X - SCREEN_SIZE.X * 0.5f + HIDDEN_PART_RIGHT, 0.5f), SCREEN_SIZE, MyGuiConstants.SCREEN_BACKGROUND_COLOR, false)
        {
            if (!Directory.Exists(m_airplaneBlueprintFolder))
            {
                Directory.CreateDirectory(m_airplaneBlueprintFolder);
            }

            if (!Directory.Exists(m_airplaneBlueprintFolder))
            {
                Directory.CreateDirectory(m_airplaneBlueprintFolder);
            }
            m_blueprintList.Items.Clear();
            GetLocalBlueprintNames(m_downloadFromSteam);

            if (m_downloadFromSteam)
            {
                m_downloadFromSteam = false;
            }

            CreateTempDirectory();

            RecreateControls(true);

            m_blueprintList.ItemsSelected += OnSelectItem;
            m_blueprintList.ItemDoubleClicked += OnItemDoubleClick;
            m_blueprintList.ItemMouseOver += OnMouseOverItem;
            OnEnterCallback += Ok;
            m_closeOnEsc = false;

            SetCameraPosition_FarAway();

            if (MySession.Static.VehicleEditor_Vehicle != null && !exitingGame)
            {
                TakeScreenshot(MySession.Static.VehicleEditor_Vehicle.DisplayName);
            }
            
        }

        /// <summary>
        /// Place a spectator camera at a set location and teleport the character behind the camera, stationary
        /// </summary>
        private void SetCameraPosition_FarAway()
        {
            if (MySession.Static.ControlledEntity != null) //Set 3rd person camera
            {
                var matrix = MySpectator.Static.GetViewMatrix();
                //matrix.M44 = 0.5f;
                //MySpectator.Static.SetViewMatrix(matrix);
                MySpectatorCameraController.Static.TurnLightOff();
                MySession.Static.SetCameraController(MyCameraControllerEnum.SpectatorFixed);
                //MySpectatorCameraController.Static.SpectatorCameraMovement = MySpectatorCameraMovementEnum.S

                MySpectator.Static.Position = new Vector3D(0, -15, -15);
                //MySpectator.Static.Target = (Vector3D)MySession.Static.VehicleEditor_Vehicle.PositionComp.GetPosition();
                MySpectator.Static.Target = new Vector3D(0, 0, 0);
            }

            positionBeforeScreen = MySession.Static.LocalCharacter.PositionComp.GetPosition();
            MySession.Static.LocalCharacter.PositionComp.SetPosition(new Vector3D(-150, -150, -150)); //Outside camera range and gravity range
            MySession.Static.LocalCharacter.Physics.SetSpeeds(new Vector3(), new Vector3());
        }


        /// <summary>
        /// Return character to original location and set camera to first person
        /// </summary>
        private void SetCameraPosition_FirstPerson()
        {
            MySession.Static.LocalCharacter.PositionComp.SetPosition(positionBeforeScreen);
            MySession.Static.LocalCharacter.Physics.SetSpeeds(new Vector3(), new Vector3());

            #region Reset Camera
            if (MySession.Static.ControlledEntity != null)
            { //we already are controlling this object

                if (MyFinalBuildConstants.IS_OFFICIAL)
                {
                    MyGuiScreenGamePlay.SetCameraController();
                }
                else
                {
                    var cameraController = MySession.Static.GetCameraControllerEnum();
                    if (cameraController != MyCameraControllerEnum.Entity && cameraController != MyCameraControllerEnum.ThirdPersonSpectator)
                    {
                        MyGuiScreenGamePlay.SetCameraController();
                    }
                    else
                    {
                        var entities = MyEntities.GetEntities().ToList();
                        int lastKnownIndex = entities.IndexOf(MySession.Static.ControlledEntity.Entity);

                        var entitiesList = new List<MyEntity>();
                        if (lastKnownIndex + 1 < entities.Count)
                            entitiesList.AddRange(entities.GetRange(lastKnownIndex + 1, entities.Count - lastKnownIndex - 1));

                        if (lastKnownIndex != -1)
                        {
                            entitiesList.AddRange(entities.GetRange(0, lastKnownIndex + 1));
                        }

                        MyCharacter newControlledObject = null;

                        for (int i = 0; i < entitiesList.Count; i++)
                        {
                            var character = entitiesList[i] as MyCharacter;
                            if (character != null && !character.IsDead && character.ControllerInfo.Controller == null)
                            {
                                newControlledObject = character;
                                break;
                            }
                        }

                        if (MySession.Static.LocalHumanPlayer != null && newControlledObject != null)
                        {
                            MySession.Static.LocalHumanPlayer.Controller.TakeControl(newControlledObject);
                        }
                    }

                    // We could have activated the cube builder in spectator, so deactivate it now
                    if (MyCubeBuilder.Static.IsActivated && !(MySession.Static.ControlledEntity is MyCharacter))
                        MyCubeBuilder.Static.Deactivate();
                }
            }
            #endregion

            
        }

        void CreateButtons()
        {
            Vector2 buttonPosition = new Vector2(-0.075f, 0.28f);
            Vector2 buttonOffset = new Vector2(0.15f, 0.035f);
            float width = 0.15f;

            m_okButton = CreateButton(width, new StringBuilder("Ok"), OnOk, textScale: m_textScale);
            m_okButton.Position = buttonPosition;

            var cancelButton = CreateButton(width, new StringBuilder("Cancel"), OnCancel, textScale: m_textScale);
            cancelButton.Position = buttonPosition + new Vector2(1f, 0f) * buttonOffset;

            m_detailsButton = CreateButton(width, new StringBuilder("Details"), OnDetails, textScale: m_textScale, enabled: false);
            m_detailsButton.Position = buttonPosition + new Vector2(0f, 1f) * buttonOffset;

            m_screenshotButton = CreateButton(width, new StringBuilder("Take Screenshot"), OnScreenshot, textScale: m_textScale, enabled: false);
            m_screenshotButton.Position = buttonPosition + new Vector2(1f, 1f) * buttonOffset;

            m_deleteButton = CreateButton(width, new StringBuilder("New Ship"), OnNewShip, true, textScale: m_textScale);
            m_deleteButton.Position = buttonPosition + new Vector2(0f, 2f) * buttonOffset;

            m_deleteButton = CreateButton(width, new StringBuilder("Delete"), OnDelete, false, textScale: m_textScale);
            m_deleteButton.Position = buttonPosition + new Vector2(1f, 2f) * buttonOffset;

            width = 0.3f;

            var reloadButton = CreateButton(width, new StringBuilder("Refresh Ships"), OnReload, textScale: m_textScale);
            reloadButton.Position = (buttonPosition + new Vector2(0f, 3f) * buttonOffset) * new Vector2(0f, 1f);
        }

        public void RefreshThumbnail()
        {
            m_selectedImage = new MyGuiControlImageButton();
            m_selectedImage.BorderTexture = MyGuiConstants.TEXTURE_RECTANGLE_DARK;

            m_thumbnailImage = new MyGuiControlImageButton();
            m_thumbnailImage.Position = new Vector2(-0.31f, -0.2f);
            m_thumbnailImage.Size = new Vector2(0.2f, 0.175f);
            m_thumbnailImage.BorderTexture = MyGuiConstants.TEXTURE_RECTANGLE_DARK;
            m_thumbnailImage.Visible = false;
        }

        public override void RecreateControls(bool constructor)
        {
            base.RecreateControls(constructor);

            MyAnalyticsHelper.ReportActivityStart(null, "show_blueprints", string.Empty, "gui", string.Empty);

            Vector2 searchPosition = new Vector2(0f, SCREEN_SIZE.Y - 1.58f);

            float hiddenPartTop = (SCREEN_SIZE.Y - 1.0f) / 2.0f;

            m_blueprintList.Position = new Vector2(0f, -0.03f);
            m_blueprintList.VisibleRowsCount = 17;
            m_blueprintList.MultiSelect = false;

            var caption = AddCaption("Planes", VRageMath.Color.White.ToVector4(), m_controlPadding + new Vector2(-HIDDEN_PART_RIGHT, hiddenPartTop));
            Controls.Add(m_blueprintList);

            RefreshThumbnail();
            Controls.Add(m_thumbnailImage);

            CreateButtons();

            var texture = MyGuiConstants.LOADING_TEXTURE;

            m_wheel = new MyGuiControlRotatingWheel(
                searchPosition + new Vector2(0.127f, 0f),
                MyGuiConstants.ROTATING_WHEEL_COLOR,
                0.28f,
                MyGuiDrawAlignEnum.HORISONTAL_CENTER_AND_VERTICAL_CENTER,
                texture,
                true,
                multipleSpinningWheels: MyPerGameSettings.GUI.MultipleSpinningWheels);
            Controls.Add(m_wheel);
            m_wheel.Visible = false;
        }

        void GetLocalBlueprintNames(bool reload = false)
        {
            GetBlueprints(m_airplaneBlueprintFolder, MyBlueprintTypeEnum.LOCAL);

            if (Task.IsComplete)
            {
                if (reload)
                {
                   //GetWorkshopBlueprints();
                }
                else
                {
                    //GetWorkshopItemsSteam();
                }
            }

            foreach (var i in m_recievedBlueprints)
            {
                m_blueprintList.Add(i);
            }
        }

        void GetBlueprints(string directory, MyBlueprintTypeEnum type)
        {
            if (!Directory.Exists(directory))
                return;
            string[] folders = Directory.GetDirectories(directory);
            List<string> fileNames = new List<string>();
            List<string> blueprintNames = new List<string>();

            foreach (var f in folders)
            {
                fileNames.Add(f + "\\bp.sbc");
                var tokens = f.Split('\\');
                blueprintNames.Add(tokens[tokens.Length - 1]);
            }

            for (int i = 0; i < blueprintNames.Count; i++)
            {
                String name = blueprintNames[i];
                var info = new MyBlueprintItemInfo(type);
                var item = new MyGuiControlListbox.Item(text: new StringBuilder(name), toolTip: name, userData: info, icon: MyGuiConstants.TEXTURE_ICON_BLUEPRINTS_LOCAL.Normal);
                m_blueprintList.Add(item);
            }
        }

        bool ValidateModInfo(MyObjectBuilder_ModInfo info)
        {
            if (info == null)
                return false;
            if (info.SubtypeName == null)
                return false;
            if (info.WorkshopId == null)
                return false;
            if (info.SteamIDOwner == null)
                return false;
            return true;
        }

        void GetWorkshopItemsSteam()
        {
            for (int i = 0; i < m_subscribedItemsList.Count; i++)
            {
                MySteamWorkshop.SubscribedItem suscribedItem = m_subscribedItemsList[i];
                MyAnalyticsHelper.ReportActivityStart(null, "show_blueprints", string.Empty, "gui", string.Empty);

                String name = suscribedItem.Title;
                var info = new MyBlueprintItemInfo(MyBlueprintTypeEnum.STEAM, suscribedItem.PublishedFileId) { Item = suscribedItem };
                var item = new MyGuiControlListbox.Item(text: new StringBuilder(name), toolTip: name, userData: info, icon: MyGuiConstants.TEXTURE_ICON_MODS_WORKSHOP.Normal);
                m_blueprintList.Add(item);
            }
        }

        void GetWorkshopItemsLocal()
        {
            var filePath = Path.Combine(m_airplaneBlueprintFolder, "temp");
            if (Directory.Exists(filePath))
            {
                var folders = Directory.GetDirectories(filePath);
                var blueprintNames = new List<string>();
                var fileNames = new List<string>();

                foreach (var f in folders)
                {
                    var tokens = f.Split('\\');
                    blueprintNames.Add(tokens[tokens.Length - 1]);
                }
                for (int i = 0; i < blueprintNames.Count; i++)
                {
                    var modInfoPath = Path.Combine(filePath, blueprintNames[i], "info.temp");
                    MyObjectBuilder_ModInfo modInfo = null;
                    if (File.Exists(modInfoPath))
                    {
                        MyAnalyticsHelper.ReportActivityStart(null, "show_blueprints", string.Empty, "gui", string.Empty);
                        var success = MyObjectBuilderSerializer.DeserializeXML(modInfoPath, out modInfo);

                        if (!ValidateModInfo(modInfo) || !success)
                            continue;

                        String name = modInfo.SubtypeName;
                        var info = new MyBlueprintItemInfo(MyBlueprintTypeEnum.STEAM, modInfo.WorkshopId);
                        var item = new MyGuiControlListbox.Item(text: new StringBuilder(name), toolTip: name, userData: info, icon: MyGuiConstants.TEXTURE_ICON_MODS_WORKSHOP.Normal);
                        m_blueprintList.Add(item);
                    }
                }
            }
        }

        bool IsExtracted(MySteamWorkshop.SubscribedItem subItem)
        {
            var extractPath = Path.Combine(TEMP_PATH, subItem.PublishedFileId.ToString());
            return Directory.Exists(extractPath);
        }

        void ExtractWorkshopItem(MySteamWorkshop.SubscribedItem subItem)
        {
            string archive = Path.Combine(m_airplaneBlueprintFolder, subItem.PublishedFileId.ToString() + m_workshopBlueprintSuffix);
            var extractPath = Path.Combine(TEMP_PATH, subItem.PublishedFileId.ToString());

            if (Directory.Exists(extractPath))
            {
                Directory.Delete(extractPath);
            }

            Directory.CreateDirectory(extractPath);
            var extracted = MyZipArchive.OpenOnFile(archive);

            var modInfo = new MyObjectBuilder_ModInfo();
            modInfo.SubtypeName = subItem.Title;
            modInfo.WorkshopId = subItem.PublishedFileId;
            modInfo.SteamIDOwner = subItem.SteamIDOwner;

            var infoFile = Path.Combine(TEMP_PATH, subItem.PublishedFileId.ToString(), "info.temp");
            if (File.Exists(infoFile))
            {
                File.Delete(infoFile);
            }

            var infoSuccess = MyObjectBuilderSerializer.SerializeXML(infoFile, false, modInfo);

            if (extracted.FileExists("thumb.png"))
            {
                var stream = extracted.GetFile("thumb.png").GetStream();
                if (stream != null)
                {
                    using (var file = File.Create(Path.Combine(extractPath, "thumb.png")))
                    {
                        stream.CopyTo(file);
                    }
                }
                stream.Close();
            }

            extracted.Dispose();

            var info = new MyBlueprintItemInfo(MyBlueprintTypeEnum.STEAM, subItem.PublishedFileId);
            var listItem = new MyGuiControlListbox.Item(text: new StringBuilder(subItem.Title), toolTip: subItem.Title, icon: MyGuiConstants.TEXTURE_ICON_MODS_WORKSHOP.Normal, userData: info);

            var itemIndex = m_blueprintList.Items.FindIndex(item => ((item.UserData as MyBlueprintItemInfo).PublishedItemId == (listItem.UserData as MyBlueprintItemInfo).PublishedItemId) && (item.UserData as MyBlueprintItemInfo).Type == MyBlueprintTypeEnum.STEAM);
            if (itemIndex == -1)
            {
                m_blueprintList.Add(listItem);
            }
        }

        DirectoryInfo CreateTempDirectory()
        {
            return Directory.CreateDirectory(TEMP_PATH);
        }

        void DownloadBlueprints()
        {
            ProfilerShort.Begin("Getting workshop blueprints.");
            m_downloadFromSteam = true;
            ProfilerShort.BeginNextBlock("downloading");
            m_subscribedItemsList.Clear();
            bool success = MySteamWorkshop.GetSubscribedBlueprintsBlocking(m_subscribedItemsList);

            if (success)
            {
                Directory.CreateDirectory(m_airplaneBlueprintFolder);

                foreach (var item in m_subscribedItemsList)
                {
                    string archive = Path.Combine(m_airplaneBlueprintFolder, item.PublishedFileId.ToString() + m_workshopBlueprintSuffix);
                    if (File.Exists(archive))
                    {
                        m_downloadFinished.Add(item.PublishedFileId);
                    }
                    else
                    {
                        DownloadBlueprintFromSteam(item);
                        m_downloadFinished.Add(item.PublishedFileId);
                    }
                }
            }
            if (success)
            {
                m_needsExtract = true;
                m_downloadFromSteam = false;
            }
            else
            {
                MyGuiSandbox.AddScreen(MyGuiSandbox.CreateMessageBox(
                    messageText: new StringBuilder("Couldn't load blueprints from steam workshop"),
                    messageCaption: new StringBuilder("Error")));
            }
            ProfilerShort.End();
        }

        void GetWorkshopBlueprints()
        {
            Task = Parallel.Start(DownloadBlueprints);
        }

        override public void RefreshBlueprintList(bool fromTask = false)
        {
            m_blueprintList.StoreSituation();
            m_blueprintList.Items.Clear();
            GetLocalBlueprintNames(fromTask);
            m_blueprintList.RestoreSituation(false, true);
        }

        void ReloadTextures()
        {
            //locals
            var path = m_airplaneBlueprintFolder;
            var files = Directory.GetDirectories(path);
            var texture = "";
            foreach (var file in files)
            {
                texture = Path.Combine(file, "thumb.png");
                if (File.Exists(texture))
                {
                    VRageRender.MyRenderProxy.UnloadTexture(texture);
                }
            }

            //default
            path = m_defaultBlueprintFolder;
            if (Directory.Exists(path))
            {
                files = Directory.GetDirectories(path);
                foreach (var file in files)
                {
                    texture = Path.Combine(file, "thumb.png");
                    if (File.Exists(texture))
                    {
                        VRageRender.MyRenderProxy.UnloadTexture(texture);
                    }
                }
            }

            //ws
            path = Path.Combine(m_airplaneBlueprintFolder, "temp");
            if (Directory.Exists(path))
            {
                files = Directory.GetDirectories(path);
                foreach (var file in files)
                {
                    texture = Path.Combine(file, "thumb.png");
                    if (File.Exists(texture))
                    {
                        VRageRender.MyRenderProxy.UnloadTexture(texture);
                    }
                }
            }
        }

        public void RefreshAndReloadBlueprintList()
        {
            m_blueprintList.StoreSituation();
            m_blueprintList.Items.Clear();
            GetLocalBlueprintNames(true);
            ReloadTextures();
            m_blueprintList.RestoreSituation(false, true);
        }

        /*void OnSearchClear(MyGuiControlButton button)
        {
            m_searchBox.Text = "";
        }*/

        void OnMouseOverItem(MyGuiControlListbox listBox)
        {
            var item = listBox.MouseOverItem;
            var path = "";
            if (item != null)
            {
                MyBlueprintItemInfo blueprintInfo = (item.UserData as MyBlueprintItemInfo);
                if (blueprintInfo.Type == MyBlueprintTypeEnum.LOCAL)
                {
                    path = Path.Combine(m_airplaneBlueprintFolder, item.Text.ToString(), "thumb.png");
                }
                else if (blueprintInfo.Type == MyBlueprintTypeEnum.STEAM)
                {
                    var id = blueprintInfo.PublishedItemId;
                    if (id != null)
                    {
                        path = Path.Combine(TEMP_PATH, id.ToString(), "thumb.png");
                        if (blueprintInfo.Item != null)
                        {
                            bool isQueued = m_downloadQueued.Contains(blueprintInfo.Item.PublishedFileId);
                            bool isDownloaded = m_downloadFinished.Contains(blueprintInfo.Item.PublishedFileId);
                            MySteamWorkshop.SubscribedItem worshopData = blueprintInfo.Item;
                            if (isDownloaded && IsExtracted(worshopData) == false)
                            {
                                m_blueprintList.Enabled = false;
                                m_okButton.Enabled = false;
                                ExtractWorkshopItem(worshopData);
                                m_blueprintList.Enabled = true;
                                m_okButton.Enabled = true;
                            }
                            if (isQueued == false && isDownloaded == false)
                            {
                                m_blueprintList.Enabled = false;
                                m_okButton.Enabled = false;
                                m_downloadQueued.Add(blueprintInfo.Item.PublishedFileId);

                                Task = Parallel.Start(() =>
                                {
                                    DownloadBlueprintFromSteam(worshopData);
                                }, () => { OnBlueprintDownloadedThumbnail(worshopData); });
                            }
                        }
                    }
                }
                else if (blueprintInfo.Type == MyBlueprintTypeEnum.DEFAULT)
                {
                    path = Path.Combine(m_defaultBlueprintFolder, item.Text.ToString(), "thumb.png");
                }

                if (File.Exists(path))
                {
                    m_thumbnailImage.SetTexture(path);
                    if (!m_activeDetail)
                    {
                        if (m_thumbnailImage.BackgroundTexture != null)
                        {
                            m_thumbnailImage.Visible = true;
                        }
                    }
                }
                else
                {
                    m_thumbnailImage.Visible = false;
                    m_thumbnailImage.BackgroundTexture = null;
                }
            }
            else
            {
                m_thumbnailImage.Visible = false;
            }
        }

        void OnSelectItem(MyGuiControlListbox list)
        {
            if (list.SelectedItems.Count == 0)
            {
                return;
            }

            m_selectedItem = list.SelectedItems[0];
            m_detailsButton.Enabled = true;
            m_screenshotButton.Enabled = true;


            var type = (m_selectedItem.UserData as MyBlueprintItemInfo).Type;
            var id = (m_selectedItem.UserData as MyBlueprintItemInfo).PublishedItemId;
            var path = "";

            if (type == MyBlueprintTypeEnum.LOCAL)
            {
                path = Path.Combine(m_airplaneBlueprintFolder, m_selectedItem.Text.ToString(), "thumb.png");
                m_deleteButton.Enabled = true;
            }
            else if (type == MyBlueprintTypeEnum.STEAM)
            {
                path = Path.Combine(m_airplaneBlueprintFolder, "temp", id.ToString(), "thumb.png");
                m_screenshotButton.Enabled = false;
                //m_replaceButton.Enabled = false;
                m_deleteButton.Enabled = false;
            }
            else if (type == MyBlueprintTypeEnum.SHARED)
            {
                //m_replaceButton.Enabled = false;
                m_screenshotButton.Enabled = false;
                m_detailsButton.Enabled = false;
                m_deleteButton.Enabled = false;
            }
            else if (type == MyBlueprintTypeEnum.DEFAULT)
            {
                path = Path.Combine(m_defaultBlueprintFolder, m_selectedItem.Text.ToString(), "thumb.png");
                //m_replaceButton.Enabled = false;
                m_screenshotButton.Enabled = false;
                m_deleteButton.Enabled = false;
            }

            if (File.Exists(path))
            {
                m_selectedImage.SetTexture(path);
            }

            else
            {
                m_selectedImage.BackgroundTexture = null;
            }
        }

        bool ValidateSelecteditem()
        {
            if (m_selectedItem == null)
                return false;
            if (m_selectedItem.UserData == null)
                return false;
            if (m_selectedItem.Text == null)
                return false;
            return true;
        }

        bool CopySelectedItemToClipboard()
        {
            if (!ValidateSelecteditem())
                return false;

            var path = "";
            MyObjectBuilder_Definitions prefab = null;
            MyBlueprintItemInfo blueprintInfo = (m_selectedItem.UserData as MyBlueprintItemInfo);
            if (blueprintInfo.Type == MyBlueprintTypeEnum.LOCAL)
            {
                path = Path.Combine(m_airplaneBlueprintFolder, m_selectedItem.Text.ToString(), "bp.sbc");
                if (File.Exists(path))
                {
                    prefab = LoadPrefab(path);
                }
            }
            else if (blueprintInfo.Type == MyBlueprintTypeEnum.STEAM)
            {
                var id = (m_selectedItem.UserData as MyBlueprintItemInfo).PublishedItemId;
                path = Path.Combine(m_airplaneBlueprintFolder, id.ToString() + m_workshopBlueprintSuffix);
                if (File.Exists(path))
                {
                    prefab = LoadWorkshopPrefab(path, id);
                }

            }
            else if (blueprintInfo.Type == MyBlueprintTypeEnum.SHARED)
            {
                return false;
            }
            else if (blueprintInfo.Type == MyBlueprintTypeEnum.DEFAULT)
            {
                path = Path.Combine(m_defaultBlueprintFolder, m_selectedItem.Text.ToString(), "bp.sbc");
                if (File.Exists(path))
                {
                    prefab = LoadPrefab(path);
                }
            }

            if (prefab != null)
            {
                if (MySandboxGame.Static.SessionCompatHelper != null)
                {
                    MySandboxGame.Static.SessionCompatHelper.CheckAndFixPrefab(prefab);
                }
                return CopyBlueprintPrefabToClipboard(prefab, m_clipboard);
            }
            else
            {
                MyGuiSandbox.AddScreen(MyGuiSandbox.CreateMessageBox(
                            buttonType: MyMessageBoxButtonsType.OK,
                            styleEnum: MyMessageBoxStyleEnum.Error,
                            messageCaption: new StringBuilder("Error"),
                            messageText: new StringBuilder("Failed to load the blueprint file.")
                            ));
                return false;
            }
        }

        public static bool CopyBlueprintPrefabToClipboard(MyObjectBuilder_Definitions prefab, MyGridClipboard clipboard, bool setOwner = true)
        {
            if (prefab.ShipBlueprints == null)
                return false;

            var cubeGrids = prefab.ShipBlueprints[0].CubeGrids;

            if (cubeGrids == null || cubeGrids.Length == 0)
                return false;

            var localBB = MyCubeGridExtensions.CalculateBoundingSphere(cubeGrids[0]);

            var posAndOrient = cubeGrids[0].PositionAndOrientation.Value;
            var worldMatrix = MatrixD.CreateWorld(posAndOrient.Position, posAndOrient.Forward, posAndOrient.Up);
            var invertedNormalizedWorld = Matrix.Normalize(Matrix.Invert(worldMatrix));

            var worldBB = localBB.Transform(worldMatrix);

            var dragVector = Vector3.TransformNormal((Vector3)(Vector3D)posAndOrient.Position - worldBB.Center, invertedNormalizedWorld);
            var dragDistance = localBB.Radius + 10f;

            //Reset ownership to local player
            if (setOwner)
            {
                foreach (var gridBuilder in cubeGrids)
                {
                    foreach (var block in gridBuilder.CubeBlocks)
                    {
                        if (block.Owner != 0)
                        {
                            block.Owner = MySession.Static.LocalPlayerId;
                        }
                    }
                }
            }

            // Blueprint can have old (deprecated) fractured blocks, they have to be converted to fracture components. There is no version in blueprints.
            if (MyFakes.ENABLE_FRACTURE_COMPONENT)
            {
                for (int i = 0; i < cubeGrids.Length; ++i)
                {
                    cubeGrids[i] = MyFracturedBlock.ConvertFracturedBlocksToComponents(cubeGrids[i]);
                }
            }

            clipboard.SetGridFromBuilders(cubeGrids, dragVector, dragDistance);
            clipboard.Deactivate();
            return true;
        }

        public static bool CopyBlueprintPrefabToVehicleEditor(MyObjectBuilder_Definitions prefab, MyGridClipboard clipboard, bool setOwner = true)
        {
            if (prefab.ShipBlueprints == null)
                return false;

            var cubeGrids = prefab.ShipBlueprints[0].CubeGrids;

            if (cubeGrids == null || cubeGrids.Length == 0)
                return false;

            var localBB = MyCubeGridExtensions.CalculateBoundingSphere(cubeGrids[0]);

            var posAndOrient = cubeGrids[0].PositionAndOrientation.Value;
            var worldMatrix = MatrixD.CreateWorld(posAndOrient.Position, posAndOrient.Forward, posAndOrient.Up);
            var invertedNormalizedWorld = Matrix.Normalize(Matrix.Invert(worldMatrix));

            var worldBB = localBB.Transform(worldMatrix);

            var dragVector = Vector3.TransformNormal((Vector3)(Vector3D)posAndOrient.Position - worldBB.Center, invertedNormalizedWorld);
            var dragDistance = localBB.Radius + 10f;

            //Reset ownership to local player
            if (setOwner)
            {
                foreach (var gridBuilder in cubeGrids)
                {
                    foreach (var block in gridBuilder.CubeBlocks)
                    {
                        if (block.Owner != 0)
                        {
                            block.Owner = MySession.Static.LocalPlayerId;
                        }
                    }
                }
            }

            // Blueprint can have old (deprecated) fractured blocks, they have to be converted to fracture components. There is no version in blueprints.
            if (MyFakes.ENABLE_FRACTURE_COMPONENT)
            {
                for (int i = 0; i < cubeGrids.Length; ++i)
                {
                    cubeGrids[i] = MyFracturedBlock.ConvertFracturedBlocksToComponents(cubeGrids[i]);
                }
            }

            clipboard.SetGridFromBuilders(cubeGrids, dragVector, dragDistance);
            clipboard.Deactivate();
            return true;
        }

        void OnSearchTextChange(MyGuiControlTextbox box)
        {
            if (box.Text != "")
            {
                String[] tmpSearch = box.Text.Split(' ');
                foreach (var item in m_blueprintList.Items)
                {
                    String tmpName = item.Text.ToString().ToLower();
                    bool add = true;
                    foreach (var search in tmpSearch)
                        if (!tmpName.Contains(search.ToLower()))
                        {
                            add = false;
                            break;
                        }
                    if (add)
                        item.Visible = true;
                    else
                        item.Visible = false;
                }
            }
            else
            {
                foreach (var item in m_blueprintList.Items)
                    item.Visible = true;
            }
        }

        void OpenSharedBlueprint(MyBlueprintItemInfo itemInfo)
        {
            MyGuiSandbox.AddScreen(MyGuiSandbox.CreateMessageBox(
            buttonType: MyMessageBoxButtonsType.YES_NO_CANCEL,
            styleEnum: MyMessageBoxStyleEnum.Info,
            messageCaption: new StringBuilder("Shared Blueprint"),
            messageText: new StringBuilder("Do you want to open this blueprint in steam workshop?"),
            callback: delegate(MyGuiScreenMessageBox.ResultEnum callbackReturn)
            {
                if (callbackReturn == MyGuiScreenMessageBox.ResultEnum.YES)
                {
                    MySteam.API.OpenOverlayUrl(string.Format("http://steamcommunity.com/sharedfiles/filedetails/?id={0}", itemInfo.PublishedItemId));
                    m_recievedBlueprints.Remove(m_selectedItem);
                    m_selectedItem = null;
                    RefreshBlueprintList();
                }
                else if (callbackReturn == MyGuiScreenMessageBox.ResultEnum.NO)
                {
                    m_recievedBlueprints.Remove(m_selectedItem);
                    m_selectedItem = null;
                    RefreshBlueprintList();
                }
            }));

        }

        void OnItemDoubleClick(MyGuiControlListbox list)
        {
            m_selectedItem = list.SelectedItems[0];
            Ok();
        }

        private void CopyBlueprintAndClose()
        {
            var close = CopySelectedItemToClipboard();
            if (close)
            {
                CloseScreen();
            }
        }

        public Boolean LoadVehicleEditorShipFromprefab()
        {
            if (m_selectedItem == null)
            {
                CloseScreen();
                return false;
            }

            if (MySession.Static.VehicleEditorMode)
            {

                if (!ValidateSelecteditem())
                    return false;

                var path = "";
                MyObjectBuilder_Definitions prefab = null;
                MyBlueprintItemInfo blueprintInfo = (m_selectedItem.UserData as MyBlueprintItemInfo);
                path = Path.Combine(m_airplaneBlueprintFolder, m_selectedItem.Text.ToString(), "bp.sbc");
                if (File.Exists(path))
                {
                    prefab = LoadPrefab(path);
                }

                //CopyBlueprintAndClose();
                if (MySession.Static.VehicleEditor_Vehicle != null)
                {
                    if (MySession.Static.VehicleEditor_Vehicle.DisplayName == prefab.ShipBlueprints[0].DisplayName)
                    {
                        return false;
                    }
                    RemoveOldShip();
                    //MyEntities.Close();
                }

                MyGuiScreenHudSpace.forceRecalculateVehicleData = true;
                List<MyCubeGrid> gridList = new List<MyCubeGrid>();
                foreach (MyObjectBuilder_CubeGrid grid in prefab.ShipBlueprints[0].CubeGrids)
                {
                    grid.EntityId = 0;
                    MyEntity entity = MyEntities.CreateFromObjectBuilder(grid);
                    MyEntities.Add(entity);
                    MyCubeGrid cubeGrid = entity as MyCubeGrid;
                    MySession.Static.VehicleEditor_Vehicle = cubeGrid;
                    MySession.Static.VehicleEditor_Vehicle.Physics.ConvertToStatic();
                    break;
                }

                MySession.Static.VehicleEditor_Vehicle.Name = m_selectedItem.Text.ToString();
                MySession.Static.VehicleEditor_Vehicle.DisplayName = m_selectedItem.Text.ToString();
                m_gridDeleted = false;
                CloseScreen();
                return true;
            }
            return false;
        }

        void Ok()
        {
            if (m_selectedItem == null )
            {
                CloseScreen();
                return;
            }
            if (MySession.Static.VehicleEditor_Vehicle != null)
            {
                if (MySession.Static.VehicleEditor_Vehicle.DisplayName == m_selectedItem.Text.ToString())
                {
                    CloseScreen();
                    return;
                }
            }

            var itemInfo = m_selectedItem.UserData as MyBlueprintItemInfo;
            if (!m_gridDeleted)
            {
                SaveVehicleEditorShip(true, true);
            }

            if (LoadVehicleEditorShipFromprefab())
            {
                return;
            }
        }

        void OnOk(MyGuiControlButton button)
        {
            Ok();
        }

        void OnCancel(MyGuiControlButton button)
        {
            CloseScreen();
        }

        void OnReload(MyGuiControlButton button)
        {
            m_selectedItem = null;
            m_detailsButton.Enabled = false;
            m_screenshotButton.Enabled = false;
            m_downloadFinished.Clear();
            m_downloadQueued.Clear();
            RefreshAndReloadBlueprintList();
        }

        void OnDetails(MyGuiControlButton button)
        {
            if (m_selectedItem == null)
            {
                if (m_activeDetail)
                {
                    MyScreenManager.RemoveScreen(m_detailScreen);
                }
                return;
            }
            else if (m_activeDetail)
            {
                MyScreenManager.RemoveScreen(m_detailScreen);
            }
            else if (!m_activeDetail)
            {
                MyBlueprintItemInfo blueprintInfo = (m_selectedItem.UserData as MyBlueprintItemInfo);
                if (blueprintInfo.Type == MyBlueprintTypeEnum.LOCAL)
                {
                    var path = Path.Combine(m_airplaneBlueprintFolder, m_selectedItem.Text.ToString(), "bp.sbc");
                    if (File.Exists(path))
                    {
                        m_thumbnailImage.Visible = false;
                        m_detailScreen = new MyGuiDetailScreenLocal(
                            callBack: delegate(MyGuiControlListbox.Item item)
                            {
                                if (item == null)
                                {
                                    m_screenshotButton.Enabled = false;
                                    m_detailsButton.Enabled = false;
                                    //m_replaceButton.Enabled = false;
                                    m_deleteButton.Enabled = false;
                                }
                                m_selectedItem = item;
                                m_activeDetail = false;
                                m_detailScreen = null;
                                if (Task.IsComplete)
                                {
                                    RefreshBlueprintList();
                                }
                            },
                            selectedItem: m_selectedItem,
                            parent: this,
                            thumbnailTexture: m_selectedImage.BackgroundTexture,
                            textScale: m_textScale
                            );
                        m_activeDetail = true;
                        MyScreenManager.InputToNonFocusedScreens = true;
                        MyScreenManager.AddScreen(m_detailScreen);
                    }
                    else
                    {
                        MyGuiSandbox.AddScreen(MyGuiSandbox.CreateMessageBox(
                                    buttonType: MyMessageBoxButtonsType.OK,
                                    styleEnum: MyMessageBoxStyleEnum.Error,
                                    messageCaption: new StringBuilder("Error"),
                                    messageText: new StringBuilder("Cannot find the blueprint file.")
                                    ));
                    }
                }
                else if (blueprintInfo.Type == MyBlueprintTypeEnum.DEFAULT)
                {
                    var path = Path.Combine(m_defaultBlueprintFolder, m_selectedItem.Text.ToString(), "bp.sbc");
                    if (File.Exists(path))
                    {
                        m_thumbnailImage.Visible = false;
                        m_detailScreen = new MyGuiDetailScreenDefault(
                            callBack: delegate(MyGuiControlListbox.Item item)
                            {
                                if (item == null)
                                {
                                    m_screenshotButton.Enabled = false;
                                    m_detailsButton.Enabled = false;
                                    //m_replaceButton.Enabled = false;
                                    m_deleteButton.Enabled = false;
                                }
                                m_selectedItem = item;
                                m_activeDetail = false;
                                m_detailScreen = null;
                                if (Task.IsComplete)
                                {
                                    RefreshBlueprintList();
                                }
                            },
                            selectedItem: m_selectedItem,
                            parent: this,
                            thumbnailTexture: m_selectedImage.BackgroundTexture,
                            textScale: m_textScale
                            );
                        m_activeDetail = true;
                        MyScreenManager.InputToNonFocusedScreens = true;
                        MyScreenManager.AddScreen(m_detailScreen);
                    }
                    else
                    {
                        MyGuiSandbox.AddScreen(MyGuiSandbox.CreateMessageBox(
                                    buttonType: MyMessageBoxButtonsType.OK,
                                    styleEnum: MyMessageBoxStyleEnum.Error,
                                    messageCaption: new StringBuilder("Error"),
                                    messageText: new StringBuilder("Cannot find the blueprint file.")
                                    ));
                    }
                }
                else if (blueprintInfo.Type == MyBlueprintTypeEnum.STEAM)
                {
                    MySteamWorkshop.SubscribedItem workshopData = blueprintInfo.Item;
                    Task = Parallel.Start(() =>
                    {
                        if (MySteamWorkshop.IsBlueprintUpToDate(workshopData) == false)
                        {
                            DownloadBlueprintFromSteam(workshopData);
                        }
                    }, () => { OnBlueprintDownloadedDetails(workshopData); });
                }
            }
        }

        void DownloadBlueprintFromSteam(MySteamWorkshop.SubscribedItem item)
        {
            if (MySteamWorkshop.IsBlueprintUpToDate(item))
            {
                return;
            }
            MySteamWorkshop.DownloadBlueprintBlocking(item, false);
            ExtractWorkshopItem(item);
        }

        void OnBlueprintDownloadedDetails(MySteamWorkshop.SubscribedItem workshopDetails)
        {
            var path2 = Path.Combine(m_airplaneBlueprintFolder, workshopDetails.PublishedFileId.ToString() + m_workshopBlueprintSuffix);
            if (File.Exists(path2))
            {
            }
            else
            {
                MyGuiSandbox.AddScreen(MyGuiSandbox.CreateMessageBox(
                            buttonType: MyMessageBoxButtonsType.OK,
                            styleEnum: MyMessageBoxStyleEnum.Error,
                            messageCaption: new StringBuilder("Error"),
                            messageText: new StringBuilder("Cannot find the blueprint file.")
                            ));
            }
        }

        void OnBlueprintDownloadedThumbnail(MySteamWorkshop.SubscribedItem item)
        {
            m_okButton.Enabled = true;
            m_blueprintList.Enabled = true;
            string path = Path.Combine(m_airplaneBlueprintFolder, "temp", item.PublishedFileId.ToString(), "thumb.png"); ;
            if (File.Exists(path))
            {
                m_thumbnailImage.SetTexture(path);
                if (!m_activeDetail)
                {
                    if (m_thumbnailImage.BackgroundTexture != null)
                    {
                        m_thumbnailImage.Visible = true;
                    }
                }
            }
            else
            {
                m_thumbnailImage.Visible = false;
                m_thumbnailImage.BackgroundTexture = null;
            }
            m_downloadQueued.Remove(item.PublishedFileId);
            m_downloadFinished.Add(item.PublishedFileId);
        }

        public void TakeScreenshot(string name)
        {
            string path = Path.Combine(m_airplaneBlueprintFolder, name, "thumb.png");
            VRageRender.MyRenderProxy.TakeScreenshot(new Vector2(0.5f, 0.5f), path, false, true, false);
            VRageRender.MyRenderProxy.UnloadTexture(path);
            m_thumbnailImage.Visible = true;
        }

        void OnScreenshot(MyGuiControlButton button)
        {
            if (m_selectedItem == null)
            {
                return;
            }
            TakeScreenshot(m_selectedItem.Text.ToString());
        }

        public void SaveVehicleEditorShip(bool withScreenshot = false, bool replace = false)
        {
            if (MySession.Static.VehicleEditor_Vehicle == null)
            {
                return;
            }

            string name = MyUtils.StripInvalidChars(MySession.Static.VehicleEditor_Vehicle.Name);
            string newName = name;
            string path = Path.Combine(m_airplaneBlueprintFolder, name);
            int index = 1;

            string imagePath = Path.Combine(path, "thumb.png");
            if (!replace)
            {
                while (MyFileSystem.DirectoryExists(path))
                {
                    newName = name + "_" + index;
                    path = Path.Combine(m_airplaneBlueprintFolder, newName);
                    index++;
                }
            }
            

            /*if (withScreenshot)
            {
                TakeScreenshot(newName);
            }*/

            var prefab = MyObjectBuilderSerializer.CreateNewObject<MyObjectBuilder_ShipBlueprintDefinition>();
            prefab.Id = new MyDefinitionId(new MyObjectBuilderType(typeof(MyObjectBuilder_ShipBlueprintDefinition)), MyUtils.StripInvalidChars(name));
            MyObjectBuilder_CubeGrid[] cubeGrids = new MyObjectBuilder_CubeGrid[1];
            prefab.CubeGrids = new MyObjectBuilder_CubeGrid[] {(MyObjectBuilder_CubeGrid)MySession.Static.VehicleEditor_Vehicle.GetObjectBuilder(),};
            prefab.RespawnShip = false;
            prefab.DisplayName = MySteam.UserName;
            prefab.OwnerSteamId = Sync.MyId;
            if (MyFakes.ENABLE_BATTLE_SYSTEM)
                prefab.Points = MyBattleHelper.GetBattlePoints(prefab.CubeGrids);
            prefab.CubeGrids[0].DisplayName = name;
            prefab.CubeGrids[0].EntityId = 0;

            var definitions = MyObjectBuilderSerializer.CreateNewObject<MyObjectBuilder_Definitions>();
            definitions.ShipBlueprints = new MyObjectBuilder_ShipBlueprintDefinition[1];
            definitions.ShipBlueprints[0] = prefab;

            SavePrefabToFile(definitions, name: MyUtils.StripInvalidChars(name), replace: replace, type: MyBlueprintTypeEnum.AIRPLANE);
            MyPrefabManager.SavePrefabToPath(MyUtils.StripInvalidChars(name), m_airplaneBlueprintFolder, prefab.CubeGrids.ToList<MyObjectBuilder_CubeGrid>());
            RefreshBlueprintList();
            
        }

        public void CreateFromClipboard(bool withScreenshot = false, bool replace = false)
        {
            if (MyCubeBuilder.Static.Clipboard.CopiedGridsName == null)
            {
                return;
            }
            string name = MyUtils.StripInvalidChars(MyCubeBuilder.Static.Clipboard.CopiedGridsName);
            string newName = name;
            string path = Path.Combine(m_airplaneBlueprintFolder, name);
            int index = 1;

            while (MyFileSystem.DirectoryExists(path))
            {
                newName = name + "_" + index;
                path = Path.Combine(m_airplaneBlueprintFolder, newName);
                index++;
            }
            string imagePath = Path.Combine(path, "thumb.png");

            if (withScreenshot)
            {
                TakeScreenshot(newName);
            }

            var prefab = MyObjectBuilderSerializer.CreateNewObject<MyObjectBuilder_ShipBlueprintDefinition>();
            prefab.Id = new MyDefinitionId(new MyObjectBuilderType(typeof(MyObjectBuilder_ShipBlueprintDefinition)), MyUtils.StripInvalidChars(name));
            prefab.CubeGrids = MyCubeBuilder.Static.Clipboard.CopiedGrids.ToArray();
            prefab.RespawnShip = false;
            prefab.DisplayName = MySteam.UserName;
            prefab.OwnerSteamId = Sync.MyId;
            if (MyFakes.ENABLE_BATTLE_SYSTEM)
                prefab.Points = MyBattleHelper.GetBattlePoints(prefab.CubeGrids);
            prefab.CubeGrids[0].DisplayName = name;

            var definitions = MyObjectBuilderSerializer.CreateNewObject<MyObjectBuilder_Definitions>();
            definitions.ShipBlueprints = new MyObjectBuilder_ShipBlueprintDefinition[1];
            definitions.ShipBlueprints[0] = prefab;

            SavePrefabToFile(definitions, replace: replace);
            RefreshBlueprintList();
        }

        void OnDelete(MyGuiControlButton button)
        {
            MyGuiSandbox.AddScreen(MyGuiSandbox.CreateMessageBox(
                buttonType: MyMessageBoxButtonsType.YES_NO,
                styleEnum: MyMessageBoxStyleEnum.Info,
                messageCaption: new StringBuilder("Delete"),
                messageText: new StringBuilder("Are you sure you want to delete this blueprint?"),
                callback: delegate(MyGuiScreenMessageBox.ResultEnum callbackReturn)
                {
                    if (callbackReturn == MyGuiScreenMessageBox.ResultEnum.YES)
                    {
                        Debug.Assert(m_selectedItem != null, "Selected item shouldnt be null");
                        if (m_selectedItem != null)
                        {
                            if (MySession.Static.VehicleEditor_Vehicle != null) //Delete current vehicle that your working on as well
                            {
                                if (MySession.Static.VehicleEditor_Vehicle.DisplayName == m_selectedItem.Text.ToString())
                                {
                                    RemoveOldShip();
                                }
                            }
                            if (DeleteBlueprint(m_selectedItem.Text.ToString(),m_airplaneBlueprintFolder))
                            {
                                m_deleteButton.Enabled = false;
                                m_detailsButton.Enabled = false;
                                m_screenshotButton.Enabled = false;
                                //m_replaceButton.Enabled = false;
                                m_selectedItem = null;
                            }

                            RefreshBlueprintList();
                        }
                    }
                }));
        }



        void OnCreate(MyGuiControlButton button)
        {
            CreateFromClipboard();
        }

        void OnNewShip(MyGuiControlButton button)
        {
            MyGuiScreenMessageBox namePrompt = null; 
            namePrompt = MyGuiSandbox.CreateMessageBox(
                buttonType: MyMessageBoxButtonsType.TEXTBOX,
                styleEnum: MyMessageBoxStyleEnum.Info,
                messageCaption: new StringBuilder("Enter Your New Ship's Name"),
                canHideOthers: false,
                callback: delegate(MyGuiScreenMessageBox.ResultEnum callbackReturn)
                {
                    if (callbackReturn == MyGuiScreenMessageBox.ResultEnum.YES)
                    {
                        if (namePrompt != null)
                        {
                            CreateNewShip(namePrompt.textboxText);
                        }
                    }
                }
                );

            MyGuiSandbox.AddScreen(namePrompt);

        }

        private void RemoveOldShip()
        {
            MySession.Static.VehicleEditor_Vehicle.OnBlockAdded -= MyGuiScreenHudSpace.forceRecalculateVehicleData_event_added;
            MySession.Static.VehicleEditor_Vehicle.OnBlockRemoved -= MyGuiScreenHudSpace.forceRecalculateVehicleData_event_removed;
            MySession.Static.VehicleEditor_Vehicle.Close(); //Remove old ship
            m_gridDeleted = true;
        }

        private void CreateNewShip(string shipName)
        {
            if (MySession.Static.VehicleEditor_Vehicle != null)
            {
                RemoveOldShip();
            }
            List<MyCubeGrid> newBaseShip = new List<MyCubeGrid>();
            MyPrefabManager.Static.SpawnPrefab(newBaseShip, "VehicleEditorBaseShipV1", new Vector3(0, 0.5, -3.3), new Vector3(0, 0, -1) * new Vector3(0, 0, -1), new Vector3(0, 1, 0) * new Vector3(0, 1, 0));
            if (newBaseShip.Count > 0)
            {
                MySession.Static.VehicleEditor_Vehicle = newBaseShip[0];
                MySession.Static.VehicleEditor_Vehicle.DisplayName = shipName;
                MySession.Static.VehicleEditor_Vehicle.Name = shipName;
                MySession.Static.VehicleEditor_Vehicle.OnBlockAdded += MyGuiScreenHudSpace.forceRecalculateVehicleData_event_added;
                MySession.Static.VehicleEditor_Vehicle.OnBlockRemoved += MyGuiScreenHudSpace.forceRecalculateVehicleData_event_removed;
                MySession.Static.VehicleEditor_Vehicle.Physics.ConvertToStatic();
                MyGuiScreenHudSpace.forceRecalculateVehicleData = true;
                SaveVehicleEditorShip(false, false);
            }

        }

        void OnReplace(MyGuiControlButton button)
        {
            if (m_selectedItem == null)
            {
                return;
            }

            MyGuiSandbox.AddScreen(MyGuiSandbox.CreateMessageBox(
                buttonType: MyMessageBoxButtonsType.YES_NO,
                styleEnum: MyMessageBoxStyleEnum.Info,
                messageCaption: MyTexts.Get(MyCommonTexts.BlueprintsMessageBoxTitle_Replace),
                messageText: MyTexts.Get(MyCommonTexts.BlueprintsMessageBoxDesc_Replace),
                callback: delegate(MyGuiScreenMessageBox.ResultEnum callbackReturn)
                {
                    if (callbackReturn == MyGuiScreenMessageBox.ResultEnum.YES)
                    {
                        string name = m_selectedItem.Text.ToString();
                        var path = Path.Combine(m_airplaneBlueprintFolder, name, "bp.sbc");
                        if (File.Exists(path))
                        {
                            var oldBlueprint = LoadPrefab(path);
                            MyCubeBuilder.Static.Clipboard.CopiedGrids[0].DisplayName = name;
                            oldBlueprint.ShipBlueprints[0].CubeGrids = MyCubeBuilder.Static.Clipboard.CopiedGrids.ToArray();

                            if (MyFakes.ENABLE_BATTLE_SYSTEM)
                                oldBlueprint.ShipBlueprints[0].Points = MyBattleHelper.GetBattlePoints(oldBlueprint.ShipBlueprints[0].CubeGrids);

                            SavePrefabToFile(oldBlueprint, replace: true);
                            RefreshBlueprintList();
                        }
                    }
                }));
        }

        protected override void OnClosed()
        {
            base.OnClosed();
            SetCameraPosition_FirstPerson();
            MyAnalyticsHelper.ReportActivityEnd(null, "show_blueprints");
            if (m_activeDetail)
            {
                m_detailScreen.CloseScreen();
            }
        }

        public override bool Update(bool hasFocus)
        {
            if (!m_blueprintList.IsMouseOver)
            {
                m_thumbnailImage.Visible = false;
            }

            if (!Task.IsComplete)
            {
                m_wheel.Visible = true;
            }
            if (Task.IsComplete)
            {
                m_wheel.Visible = false;
                if (m_needsExtract)
                {
                    GetWorkshopItemsSteam();
                    m_needsExtract = false;
                    RefreshBlueprintList();
                }
            }
            return base.Update(hasFocus);
        }

        public override bool CloseScreen()
        {
            return base.CloseScreen();
        }

        public override void HandleInput(bool receivedFocusInThisUpdate)
        {
            base.HandleInput(receivedFocusInThisUpdate);

            /*if (MyInput.Static.IsNewKeyPressed(MyKeys.F12) || MyInput.Static.IsNewKeyPressed(MyKeys.F11) || MyInput.Static.IsNewKeyPressed(MyKeys.F10))
            {
                this.CloseScreen();
            }*/
        }
    }
}
