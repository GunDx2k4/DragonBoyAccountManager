
namespace DragonBoyAccountManager.Core.Interop.Schema;

public sealed record CharLayout(
    int Hp,
    int HpFull,
    int Mp,
    int MpFull,
    int Power,
    int Gender,
    int X,
    int Y,
    int StatusMe,
    int CharId,
    int Name,
    int MySkill,
    int VSkillFight,
    int ArrItemBag,
    int MobFocus,
    int CharFocus,
    int NpcFocus,
    int ItemFocus,
    int CurrentMovePoint,
    int EndMovePointCommand,
    int IsTeleport,
    int Cdir,
    int Cvx,
    int Cvy,
    int CxSend,
    int CySend,
    int CTypePk,
    int IsPet,
    int IsMiniPet,
    int IsManualFocus,
    int MyChar,
    int IsChangingMap,
    int IsLoadingMap,
    int VItemTime,
    int IsPaintAura,
    int IsPaintAura2);

public sealed record TileMapLayout(
    int MapId,
    int ZoneId,
    int MapName,
    int Pxw,
    int Pxh,
    int Tmw,
    int Tmh,
    int Types,
    int Maps,
    int VGo,
    int MapNames);

public sealed record GameScrLayout(
    int Instance,
    int CanAutoPlay,
    int IsAutoPlay,
    int Auto,
    int KeySkill,
    int LastSkill,
    int VMob,
    int VNpc,
    int VCharInMap,
    int VItemMap,
    int Zones,
    int OnScreenSkill,
    int TextTime,
    int IsPaintChatVip);

public sealed record GameMidletLayout(
    int Version,
    int IntVersion,
    int Ip,
    int Port);

public sealed record MobLayout(
    int MobId,
    int TemplateId,
    int Hp,
    int MaxHp,
    int X,
    int Y,
    int XFirst,
    int YFirst,
    int Status,
    int IsBoss,
    int LevelBoss,
    int IsMobMe,
    int ArrMobTemplate);

public sealed record MobTemplateLayout(
    int Name,
    int Type);

public sealed record NpcLayout(
    int NpcId,
    int Template,
    int Cx,
    int Cy,
    int ArrNpcTemplate);

public sealed record NpcTemplateLayout(
    int Name,
    int NpcTemplateId);

public sealed record SkillLayout(
    int SkillId,
    int Point,
    int CoolDown,
    int LastTimeUseThisSkill,
    int ManaUse,
    int Template);

public sealed record SkillTemplateLayout(
    int Id,
    int Name,
    int IconId);

public sealed record ItemLayout(
    int Template,
    int Quantity,
    int IndexUi);

public sealed record ItemTemplateLayout(
    int Id,
    int Type,
    int Name);

public sealed record ItemTimeLayout(
    int IdIcon);

public sealed record ItemMapLayout(
    int ItemMapId,
    int Template,
    int X,
    int Y,
    int PlayerId);

public sealed record WaypointLayout(
    int MinX,
    int MinY,
    int MaxX,
    int MaxY,
    int IsEnter,
    int IsOffline,
    int Popup);

public sealed record PopUpLayout(
    int Says);

public sealed record MovePointLayout(
    int XEnd,
    int YEnd);

public sealed record MyVectorLayout(
    int A);

public sealed record GameCanvasLayout(
    int Instance,
    int GameCanvas,
    int CurrentScreen,
    int CurrentDialog,
    int ServerScreen,
    int LoginScr,
    int IsLoading,
    int GameTick,
    int IsPlaySound,
    int LowGraphic,
    int W,
    int H,
    int HW,
    int HH,
    int Panel);

public sealed record PanelLayout(
    int IsShow);

public sealed record ScaleGUILayout(
    int ScaleScreen,
    int Width,
    int Height);

public sealed record MotherCanvasLayout(
    int Instance,
    int ZoomLevel);

public sealed record ServerListScreenLayout(
    int WaitToLogin,
    int LoadScreen,
    int BigOk,
    int Percent,
    int Cmd,
    int IsGetData,
    int IpSelect,
    int NameServer);

public sealed record MainLayout(
    int IsQuitApp,
    int Started);

public sealed record SplashScrLayout(
    int NData,
    int MaxData);

public sealed record SessionLayout(
    int Connected);

public sealed record LoginScrLayout(
    int TfUser,
    int TfPass,
    int IsCheck,
    int IsLogin2,
    int IsUpdateAll,
    int IsUpdateData,
    int IsUpdateMap,
    int IsUpdateSkill,
    int IsUpdateItem);

public sealed record CommandLayout(
    int Caption);

public sealed record TFieldLayout(
    int Text,
    int PaintedText,
    int InputType,
    int CaretPos,
    int PasswordText);

public sealed record MsgDlgLayout(
    int IsWait,
    int Info);

public sealed record RmsLayout(
    int RMS_acc,
    int RMS_pass);

public sealed record ControllerLayout(
    int IsStopReadMessage,
    int IsLoadingData);

public sealed record AutoTrainMenuLayout(
    int Instance,
    int IsChecked,
    int IsCheckedSkill,
    int IsCheckAutoPean,
    int PeanHpPercent,
    int PeanMpPercent,
    int IsCheckAllMob,
    int IsCheckedMobName,
    int ListMobNames,
    int ListSkills,
    int IsCheckAttackByHp,
    int HpQuaiOn,
    int HpQuaiDuoi,
    int IsCheckItem,
    int IsCheckPick,
    int IsCheckBua,
    int MuaBuaKills,
    int LastTimeUseItem,
    int IsNhat,
    int StrIdNhat,
    int StrIdLoaiTru,
    int StrIdVutBo,
    int TfHpOn,
    int TfHpDuoi,
    int TfHpPean,
    int TfMpPean,
    int TfIdNhat,
    int TfIdLoaiTru,
    int TfIdVutBo,
    int TfBuaKills);

public sealed record ServiceLayout(
    int Instance);

public sealed record MResourcesLayout(
    int AutoTrainTabNames,
    int AutoTrainTabNames2,
    int AutoPickNames);

public sealed record MGraphicsLayout(
    int ZoomLevel);

public sealed class GameMemoryLayout
{
    public CharLayout Char { get; }
    public TileMapLayout TileMap { get; }
    public GameScrLayout GameScr { get; }
    public GameMidletLayout GameMidlet { get; }
    public MobLayout Mob { get; }
    public MobTemplateLayout MobTemplate { get; }
    public NpcLayout Npc { get; }
    public NpcTemplateLayout NpcTemplate { get; }
    public SkillLayout Skill { get; }
    public SkillTemplateLayout SkillTemplate { get; }
    public ItemLayout Item { get; }
    public ItemTemplateLayout ItemTemplate { get; }
    public ItemTimeLayout ItemTime { get; }
    public ItemMapLayout ItemMap { get; }
    public WaypointLayout Waypoint { get; }
    public PopUpLayout PopUp { get; }
    public MovePointLayout MovePoint { get; }
    public MyVectorLayout MyVector { get; }
    public GameCanvasLayout GameCanvas { get; }
    public PanelLayout Panel { get; }
    public ServerListScreenLayout ServerListScreen { get; }
    public LoginScrLayout LoginScr { get; }
    public CommandLayout Command { get; }
    public TFieldLayout TField { get; }
    public MsgDlgLayout MsgDlg { get; }
    public RmsLayout Rms { get; }
    public ControllerLayout Controller { get; }
    public ServiceLayout Service { get; }
    public MainLayout Main { get; }
    public SplashScrLayout SplashScr { get; }
    public SessionLayout Session { get; }
    public AutoTrainMenuLayout AutoTrainMenu { get; }
    public MResourcesLayout MResources { get; }
    public MGraphicsLayout MGraphics { get; }
    public ScaleGUILayout ScaleGUI { get; }
    public MotherCanvasLayout MotherCanvas { get; }

    public long CharRva { get; }
    public long TileMapRva { get; }
    public long GameScrRva { get; }
    public long GameCanvasRva { get; }
    public long GameMidletRva { get; }
    public long MainRva { get; }
    public long RmsRva { get; }
    public long ServiceRva { get; }
    public long AutoTrainMenuRva { get; }
    public long MResourcesRva { get; }
    public long MGraphicsRva { get; }

    public GameMemoryLayout(GameVersionSchema schema)
    {
        ArgumentNullException.ThrowIfNull(schema);

        Char = new CharLayout(
            Hp: ResolveInstance(schema, GameSchema.Char.ClassName, GameSchema.Char.FieldHp),
            HpFull: ResolveInstance(schema, GameSchema.Char.ClassName, GameSchema.Char.FieldHpFull),
            Mp: ResolveInstance(schema, GameSchema.Char.ClassName, GameSchema.Char.FieldMp),
            MpFull: ResolveInstance(schema, GameSchema.Char.ClassName, GameSchema.Char.FieldMpFull),
            Power: ResolveInstance(schema, GameSchema.Char.ClassName, GameSchema.Char.FieldPower),
            Gender: ResolveInstance(schema, GameSchema.Char.ClassName, GameSchema.Char.FieldGender),
            X: ResolveInstance(schema, GameSchema.Char.ClassName, GameSchema.Char.FieldX),
            Y: ResolveInstance(schema, GameSchema.Char.ClassName, GameSchema.Char.FieldY),
            StatusMe: ResolveInstance(schema, GameSchema.Char.ClassName, GameSchema.Char.FieldStatusMe),
            CharId: ResolveInstance(schema, GameSchema.Char.ClassName, GameSchema.Char.FieldCharId),
            Name: ResolveInstance(schema, GameSchema.Char.ClassName, GameSchema.Char.FieldName),
            MySkill: ResolveInstance(schema, GameSchema.Char.ClassName, GameSchema.Char.FieldMySkill),
            VSkillFight: ResolveInstance(schema, GameSchema.Char.ClassName, GameSchema.Char.FieldVSkillFight),
            ArrItemBag: ResolveInstance(schema, GameSchema.Char.ClassName, GameSchema.Char.FieldArrItemBag),
            MobFocus: ResolveInstance(schema, GameSchema.Char.ClassName, GameSchema.Char.FieldMobFocus),
            CharFocus: ResolveInstance(schema, GameSchema.Char.ClassName, GameSchema.Char.FieldCharFocus),
            NpcFocus: ResolveInstance(schema, GameSchema.Char.ClassName, GameSchema.Char.FieldNpcFocus),
            ItemFocus: ResolveInstance(schema, GameSchema.Char.ClassName, GameSchema.Char.FieldItemFocus),
            CurrentMovePoint: ResolveInstance(schema, GameSchema.Char.ClassName, GameSchema.Char.FieldCurrentMovePoint),
            EndMovePointCommand: ResolveInstance(schema, GameSchema.Char.ClassName, GameSchema.Char.FieldEndMovePointCommand),
            IsTeleport: ResolveInstance(schema, GameSchema.Char.ClassName, GameSchema.Char.FieldIsTeleport),
            Cdir: ResolveInstance(schema, GameSchema.Char.ClassName, GameSchema.Char.FieldCdir),
            Cvx: ResolveInstance(schema, GameSchema.Char.ClassName, GameSchema.Char.FieldCvx),
            Cvy: ResolveInstance(schema, GameSchema.Char.ClassName, GameSchema.Char.FieldCvy),
            CxSend: ResolveInstance(schema, GameSchema.Char.ClassName, GameSchema.Char.FieldCxSend),
            CySend: ResolveInstance(schema, GameSchema.Char.ClassName, GameSchema.Char.FieldCySend),
            CTypePk: ResolveInstance(schema, GameSchema.Char.ClassName, GameSchema.Char.FieldCTypePk),
            IsPet: ResolveInstance(schema, GameSchema.Char.ClassName, GameSchema.Char.FieldIsPet),
            IsMiniPet: ResolveInstance(schema, GameSchema.Char.ClassName, GameSchema.Char.FieldIsMiniPet),
            IsManualFocus: ResolveInstance(schema, GameSchema.Char.ClassName, GameSchema.Char.FieldIsManualFocus),
            MyChar: ResolveStatic(schema, GameSchema.Char.ClassName, GameSchema.Char.StaticMyChar),
            IsChangingMap: ResolveStatic(schema, GameSchema.Char.ClassName, GameSchema.Char.StaticIsChangingMap),
            IsLoadingMap: ResolveStatic(schema, GameSchema.Char.ClassName, GameSchema.Char.StaticIsLoadingMap),
            VItemTime: ResolveStatic(schema, GameSchema.Char.ClassName, GameSchema.Char.StaticVItemTime),
            IsPaintAura: ResolveStatic(schema, GameSchema.Char.ClassName, GameSchema.Char.StaticIsPaintAura),
            IsPaintAura2: ResolveStatic(schema, GameSchema.Char.ClassName, GameSchema.Char.StaticIsPaintAura2)
        );

        TileMap = new TileMapLayout(
            MapId: ResolveStatic(schema, GameSchema.TileMap.ClassName, GameSchema.TileMap.StaticMapId),
            ZoneId: ResolveStatic(schema, GameSchema.TileMap.ClassName, GameSchema.TileMap.StaticZoneId),
            MapName: ResolveStatic(schema, GameSchema.TileMap.ClassName, GameSchema.TileMap.StaticMapName),
            Pxw: ResolveStatic(schema, GameSchema.TileMap.ClassName, GameSchema.TileMap.StaticPxw),
            Pxh: ResolveStatic(schema, GameSchema.TileMap.ClassName, GameSchema.TileMap.StaticPxh),
            Tmw: ResolveStatic(schema, GameSchema.TileMap.ClassName, GameSchema.TileMap.StaticTmw),
            Tmh: ResolveStatic(schema, GameSchema.TileMap.ClassName, GameSchema.TileMap.StaticTmh),
            Types: ResolveStatic(schema, GameSchema.TileMap.ClassName, GameSchema.TileMap.StaticTypes),
            Maps: ResolveStatic(schema, GameSchema.TileMap.ClassName, GameSchema.TileMap.StaticMaps),
            VGo: ResolveStatic(schema, GameSchema.TileMap.ClassName, GameSchema.TileMap.StaticVGo),
            MapNames: ResolveStatic(schema, GameSchema.TileMap.ClassName, GameSchema.TileMap.StaticMapNames)
        );

        GameScr = new GameScrLayout(
            Instance: ResolveStatic(schema, GameSchema.GameScr.ClassName, GameSchema.GameScr.StaticInstance),
            CanAutoPlay: ResolveStatic(schema, GameSchema.GameScr.ClassName, GameSchema.GameScr.StaticCanAutoPlay),
            IsAutoPlay: ResolveStatic(schema, GameSchema.GameScr.ClassName, GameSchema.GameScr.StaticIsAutoPlay),
            Auto: ResolveInstance(schema, GameSchema.GameScr.ClassName, GameSchema.GameScr.FieldAuto),
            KeySkill: ResolveStatic(schema, GameSchema.GameScr.ClassName, GameSchema.GameScr.StaticKeySkill),
            LastSkill: ResolveInstance(schema, GameSchema.GameScr.ClassName, GameSchema.GameScr.FieldLastSkill),
            VMob: ResolveStatic(schema, GameSchema.GameScr.ClassName, GameSchema.GameScr.StaticVMob),
            VNpc: ResolveStatic(schema, GameSchema.GameScr.ClassName, GameSchema.GameScr.StaticVNpc),
            VCharInMap: ResolveStatic(schema, GameSchema.GameScr.ClassName, GameSchema.GameScr.StaticVCharInMap),
            VItemMap: ResolveStatic(schema, GameSchema.GameScr.ClassName, GameSchema.GameScr.StaticVItemMap),
            Zones: ResolveInstance(schema, GameSchema.GameScr.ClassName, GameSchema.GameScr.FieldZones),
            OnScreenSkill: ResolveStatic(schema, GameSchema.GameScr.ClassName, GameSchema.GameScr.StaticOnScreenSkill),
            TextTime: ResolveStatic(schema, GameSchema.GameScr.ClassName, GameSchema.GameScr.StaticTextTime),
            IsPaintChatVip: ResolveStatic(schema, GameSchema.GameScr.ClassName, GameSchema.GameScr.StaticIsPaintChatVip)
        );

        GameMidlet = new GameMidletLayout(
            Version: ResolveStatic(schema, GameSchema.GameMidlet.ClassName, GameSchema.GameMidlet.StaticVersion),
            IntVersion: ResolveStatic(schema, GameSchema.GameMidlet.ClassName, GameSchema.GameMidlet.StaticIntVersion),
            Ip: ResolveStatic(schema, GameSchema.GameMidlet.ClassName, GameSchema.GameMidlet.StaticIp),
            Port: ResolveStatic(schema, GameSchema.GameMidlet.ClassName, GameSchema.GameMidlet.StaticPort)
        );

        Mob = new MobLayout(
            MobId: ResolveInstance(schema, GameSchema.Mob.ClassName, GameSchema.Mob.FieldMobId),
            TemplateId: ResolveInstance(schema, GameSchema.Mob.ClassName, GameSchema.Mob.FieldTemplateId),
            Hp: ResolveInstance(schema, GameSchema.Mob.ClassName, GameSchema.Mob.FieldHp),
            MaxHp: ResolveInstance(schema, GameSchema.Mob.ClassName, GameSchema.Mob.FieldMaxHp),
            X: ResolveInstance(schema, GameSchema.Mob.ClassName, GameSchema.Mob.FieldX),
            Y: ResolveInstance(schema, GameSchema.Mob.ClassName, GameSchema.Mob.FieldY),
            XFirst: ResolveInstance(schema, GameSchema.Mob.ClassName, GameSchema.Mob.FieldXFirst),
            YFirst: ResolveInstance(schema, GameSchema.Mob.ClassName, GameSchema.Mob.FieldYFirst),
            Status: ResolveInstance(schema, GameSchema.Mob.ClassName, GameSchema.Mob.FieldStatus),
            IsBoss: ResolveInstance(schema, GameSchema.Mob.ClassName, GameSchema.Mob.FieldIsBoss),
            LevelBoss: ResolveInstance(schema, GameSchema.Mob.ClassName, GameSchema.Mob.FieldLevelBoss),
            IsMobMe: ResolveInstance(schema, GameSchema.Mob.ClassName, GameSchema.Mob.FieldIsMobMe),
            ArrMobTemplate: ResolveStatic(schema, GameSchema.Mob.ClassName, GameSchema.Mob.StaticArrMobTemplate)
        );

        MobTemplate = new MobTemplateLayout(
            Name: ResolveInstance(schema, GameSchema.MobTemplate.ClassName, GameSchema.MobTemplate.FieldName),
            Type: ResolveInstance(schema, GameSchema.MobTemplate.ClassName, GameSchema.MobTemplate.FieldType)
        );

        Npc = new NpcLayout(
            NpcId: ResolveInstance(schema, GameSchema.Npc.ClassName, GameSchema.Npc.FieldNpcId),
            Template: ResolveInstance(schema, GameSchema.Npc.ClassName, GameSchema.Npc.FieldTemplate),
            Cx: ResolveInstance(schema, GameSchema.Npc.ClassName, GameSchema.Npc.FieldCx, ResolveInstance(schema, GameSchema.Char.ClassName, GameSchema.Char.FieldX)),
            Cy: ResolveInstance(schema, GameSchema.Npc.ClassName, GameSchema.Npc.FieldCy, ResolveInstance(schema, GameSchema.Char.ClassName, GameSchema.Char.FieldY)),
            ArrNpcTemplate: ResolveStatic(schema, GameSchema.Npc.ClassName, GameSchema.Npc.StaticArrNpcTemplate)
        );

        NpcTemplate = new NpcTemplateLayout(
            Name: ResolveInstance(schema, GameSchema.NpcTemplate.ClassName, GameSchema.NpcTemplate.FieldName),
            NpcTemplateId: ResolveInstance(schema, GameSchema.NpcTemplate.ClassName, GameSchema.NpcTemplate.FieldNpcTemplateId)
        );

        Skill = new SkillLayout(
            SkillId: ResolveInstance(schema, GameSchema.Skill.ClassName, GameSchema.Skill.FieldSkillId),
            Point: ResolveInstance(schema, GameSchema.Skill.ClassName, GameSchema.Skill.FieldPoint),
            CoolDown: ResolveInstance(schema, GameSchema.Skill.ClassName, GameSchema.Skill.FieldCoolDown),
            LastTimeUseThisSkill: ResolveInstance(schema, GameSchema.Skill.ClassName, GameSchema.Skill.FieldLastTimeUseThisSkill),
            ManaUse: ResolveInstance(schema, GameSchema.Skill.ClassName, GameSchema.Skill.FieldManaUse),
            Template: ResolveInstance(schema, GameSchema.Skill.ClassName, GameSchema.Skill.FieldTemplate)
        );

        SkillTemplate = new SkillTemplateLayout(
            Id: ResolveInstance(schema, GameSchema.SkillTemplate.ClassName, GameSchema.SkillTemplate.FieldId),
            Name: ResolveInstance(schema, GameSchema.SkillTemplate.ClassName, GameSchema.SkillTemplate.FieldName),
            IconId: ResolveInstance(schema, GameSchema.SkillTemplate.ClassName, GameSchema.SkillTemplate.FieldIconId)
        );

        Item = new ItemLayout(
            Template: ResolveInstance(schema, GameSchema.Item.ClassName, GameSchema.Item.FieldTemplate),
            Quantity: ResolveInstance(schema, GameSchema.Item.ClassName, GameSchema.Item.FieldQuantity),
            IndexUi: ResolveInstance(schema, GameSchema.Item.ClassName, GameSchema.Item.FieldIndexUi)
        );

        ItemTemplate = new ItemTemplateLayout(
            Id: ResolveInstance(schema, GameSchema.ItemTemplate.ClassName, GameSchema.ItemTemplate.FieldId),
            Type: ResolveInstance(schema, GameSchema.ItemTemplate.ClassName, GameSchema.ItemTemplate.FieldType),
            Name: ResolveInstance(schema, GameSchema.ItemTemplate.ClassName, GameSchema.ItemTemplate.FieldName)
        );

        ItemTime = new ItemTimeLayout(
            IdIcon: ResolveInstance(schema, GameSchema.ItemTime.ClassName, GameSchema.ItemTime.FieldIdIcon)
        );

        ItemMap = new ItemMapLayout(
            ItemMapId: ResolveInstance(schema, GameSchema.ItemMap.ClassName, GameSchema.ItemMap.FieldItemMapId),
            Template: ResolveInstance(schema, GameSchema.ItemMap.ClassName, GameSchema.ItemMap.FieldTemplate),
            X: ResolveInstance(schema, GameSchema.ItemMap.ClassName, GameSchema.ItemMap.FieldX),
            Y: ResolveInstance(schema, GameSchema.ItemMap.ClassName, GameSchema.ItemMap.FieldY),
            PlayerId: ResolveInstance(schema, GameSchema.ItemMap.ClassName, GameSchema.ItemMap.FieldPlayerId)
        );

        Waypoint = new WaypointLayout(
            MinX: ResolveInstance(schema, GameSchema.Waypoint.ClassName, GameSchema.Waypoint.FieldMinX),
            MinY: ResolveInstance(schema, GameSchema.Waypoint.ClassName, GameSchema.Waypoint.FieldMinY),
            MaxX: ResolveInstance(schema, GameSchema.Waypoint.ClassName, GameSchema.Waypoint.FieldMaxX),
            MaxY: ResolveInstance(schema, GameSchema.Waypoint.ClassName, GameSchema.Waypoint.FieldMaxY),
            IsEnter: ResolveInstance(schema, GameSchema.Waypoint.ClassName, GameSchema.Waypoint.FieldIsEnter),
            IsOffline: ResolveInstance(schema, GameSchema.Waypoint.ClassName, GameSchema.Waypoint.FieldIsOffline),
            Popup: ResolveInstance(schema, GameSchema.Waypoint.ClassName, GameSchema.Waypoint.FieldPopup)
        );

        PopUp = new PopUpLayout(
            Says: ResolveInstance(schema, GameSchema.PopUp.ClassName, GameSchema.PopUp.FieldSays)
        );

        MovePoint = new MovePointLayout(
            XEnd: ResolveInstance(schema, GameSchema.MovePoint.ClassName, GameSchema.MovePoint.FieldXEnd),
            YEnd: ResolveInstance(schema, GameSchema.MovePoint.ClassName, GameSchema.MovePoint.FieldYEnd)
        );

        MyVector = new MyVectorLayout(
            A: ResolveInstance(schema, GameSchema.MyVector.ClassName, GameSchema.MyVector.FieldA)
        );

        GameCanvas = new GameCanvasLayout(
            Instance: ResolveStatic(schema, GameSchema.GameCanvas.ClassName, GameSchema.GameCanvas.StaticInstance),
            GameCanvas: ResolveStatic(schema, GameSchema.GameCanvas.ClassName, GameSchema.GameCanvas.StaticGameCanvas),
            CurrentScreen: ResolveStatic(schema, GameSchema.GameCanvas.ClassName, GameSchema.GameCanvas.StaticCurrentScreen),
            CurrentDialog: ResolveStatic(schema, GameSchema.GameCanvas.ClassName, GameSchema.GameCanvas.StaticCurrentDialog),
            ServerScreen: ResolveStatic(schema, GameSchema.GameCanvas.ClassName, GameSchema.GameCanvas.StaticServerScreen),
            LoginScr: ResolveStatic(schema, GameSchema.GameCanvas.ClassName, GameSchema.GameCanvas.StaticLoginScr),
            IsLoading: ResolveStatic(schema, GameSchema.GameCanvas.ClassName, GameSchema.GameCanvas.StaticIsLoading),
            GameTick: ResolveStatic(schema, GameSchema.GameCanvas.ClassName, GameSchema.GameCanvas.StaticGameTick),
            IsPlaySound: ResolveStatic(schema, GameSchema.GameCanvas.ClassName, GameSchema.GameCanvas.StaticIsPlaySound),
            LowGraphic: ResolveStatic(schema, GameSchema.GameCanvas.ClassName, GameSchema.GameCanvas.StaticLowGraphic),
            W: ResolveStatic(schema, GameSchema.GameCanvas.ClassName, GameSchema.GameCanvas.StaticW),
            H: ResolveStatic(schema, GameSchema.GameCanvas.ClassName, GameSchema.GameCanvas.StaticH),
            HW: ResolveStatic(schema, GameSchema.GameCanvas.ClassName, GameSchema.GameCanvas.StaticHw),
            HH: ResolveStatic(schema, GameSchema.GameCanvas.ClassName, GameSchema.GameCanvas.StaticHh),
            Panel: ResolveStatic(schema, GameSchema.GameCanvas.ClassName, GameSchema.GameCanvas.StaticPanel)
        );

        Panel = new PanelLayout(
            IsShow: ResolveInstance(schema, GameSchema.Panel.ClassName, GameSchema.Panel.FieldIsShow)
        );

        ServerListScreen = new ServerListScreenLayout(
            WaitToLogin: ResolveStatic(schema, GameSchema.ServerListScreen.ClassName, GameSchema.ServerListScreen.StaticWaitToLogin),
            LoadScreen: ResolveStatic(schema, GameSchema.ServerListScreen.ClassName, GameSchema.ServerListScreen.StaticLoadScreen),
            BigOk: ResolveStatic(schema, GameSchema.ServerListScreen.ClassName, GameSchema.ServerListScreen.StaticBigOk),
            Percent: ResolveStatic(schema, GameSchema.ServerListScreen.ClassName, GameSchema.ServerListScreen.StaticPercent),
            Cmd: ResolveInstance(schema, GameSchema.ServerListScreen.ClassName, GameSchema.ServerListScreen.FieldCmd),
            IsGetData: ResolveStatic(schema, GameSchema.ServerListScreen.ClassName, GameSchema.ServerListScreen.StaticIsGetData),
            IpSelect: ResolveStatic(schema, GameSchema.ServerListScreen.ClassName, GameSchema.ServerListScreen.StaticIpSelect),
            NameServer: ResolveStatic(schema, GameSchema.ServerListScreen.ClassName, GameSchema.ServerListScreen.StaticNameServer)
        );

        LoginScr = new LoginScrLayout(
            TfUser: ResolveInstance(schema, GameSchema.LoginScr.ClassName, GameSchema.LoginScr.FieldTfUser),
            TfPass: ResolveInstance(schema, GameSchema.LoginScr.ClassName, GameSchema.LoginScr.FieldTfPass),
            IsCheck: ResolveInstance(schema, GameSchema.LoginScr.ClassName, GameSchema.LoginScr.FieldIsCheck),
            IsLogin2: ResolveInstance(schema, GameSchema.LoginScr.ClassName, GameSchema.LoginScr.FieldIsLogin2),
            IsUpdateAll: ResolveStatic(schema, GameSchema.LoginScr.ClassName, GameSchema.LoginScr.StaticIsUpdateAll),
            IsUpdateData: ResolveStatic(schema, GameSchema.LoginScr.ClassName, GameSchema.LoginScr.StaticIsUpdateData),
            IsUpdateMap: ResolveStatic(schema, GameSchema.LoginScr.ClassName, GameSchema.LoginScr.StaticIsUpdateMap),
            IsUpdateSkill: ResolveStatic(schema, GameSchema.LoginScr.ClassName, GameSchema.LoginScr.StaticIsUpdateSkill),
            IsUpdateItem: ResolveStatic(schema, GameSchema.LoginScr.ClassName, GameSchema.LoginScr.StaticIsUpdateItem)
        );

        Command = new CommandLayout(
            Caption: ResolveInstance(schema, GameSchema.Command.ClassName, GameSchema.Command.FieldCaption)
        );

        TField = new TFieldLayout(
            Text: ResolveInstance(schema, GameSchema.TField.ClassName, GameSchema.TField.FieldText),
            PaintedText: ResolveInstance(schema, GameSchema.TField.ClassName, GameSchema.TField.FieldPaintedText),
            InputType: ResolveInstance(schema, GameSchema.TField.ClassName, GameSchema.TField.FieldInputType),
            CaretPos: ResolveInstance(schema, GameSchema.TField.ClassName, GameSchema.TField.FieldCaretPos),
            PasswordText: ResolveInstance(schema, GameSchema.TField.ClassName, GameSchema.TField.FieldPasswordText)
        );

        MsgDlg = new MsgDlgLayout(
            IsWait: ResolveInstance(schema, GameSchema.MsgDlg.ClassName, GameSchema.MsgDlg.FieldIsWait),
            Info: ResolveInstance(schema, GameSchema.MsgDlg.ClassName, GameSchema.MsgDlg.FieldInfo)
        );

        Rms = new RmsLayout(
            RMS_acc: ResolveStatic(schema, GameSchema.Rms.ClassName, GameSchema.Rms.StaticRMS_acc),
            RMS_pass: ResolveStatic(schema, GameSchema.Rms.ClassName, GameSchema.Rms.StaticRMS_pass)
        );

        Controller = new ControllerLayout(
            IsStopReadMessage: ResolveStatic(schema, GameSchema.Controller.ClassName, GameSchema.Controller.StaticIsStopReadMessage),
            IsLoadingData: ResolveStatic(schema, GameSchema.Controller.ClassName, GameSchema.Controller.StaticIsLoadingData)
        );

        Service = new ServiceLayout(
            Instance: ResolveStatic(schema, GameSchema.Service.ClassName, GameSchema.Service.StaticInstance)
        );

        Main = new MainLayout(
            IsQuitApp: ResolveStatic(schema, GameSchema.Main.ClassName, GameSchema.Main.StaticIsQuitApp),
            Started: ResolveStatic(schema, GameSchema.Main.ClassName, GameSchema.Main.StaticStarted)
        );

        SplashScr = new SplashScrLayout(
            NData: ResolveStatic(schema, GameSchema.SplashScr.ClassName, GameSchema.SplashScr.StaticNData),
            MaxData: ResolveStatic(schema, GameSchema.SplashScr.ClassName, GameSchema.SplashScr.StaticMaxData)
        );

        Session = new SessionLayout(
            Connected: ResolveStatic(schema, GameSchema.Session.ClassName, GameSchema.Session.StaticConnected)
        );

        AutoTrainMenu = new AutoTrainMenuLayout(
            Instance: ResolveStatic(schema, GameSchema.AutoTrainMenu.ClassName, GameSchema.AutoTrainMenu.StaticInstance),
            IsChecked: ResolveStatic(schema, GameSchema.AutoTrainMenu.ClassName, GameSchema.AutoTrainMenu.StaticIsChecked),
            IsCheckedSkill: ResolveStatic(schema, GameSchema.AutoTrainMenu.ClassName, GameSchema.AutoTrainMenu.StaticIsCheckedSkill),
            IsCheckAutoPean: ResolveStatic(schema, GameSchema.AutoTrainMenu.ClassName, GameSchema.AutoTrainMenu.StaticIsCheckAutoPean),
            PeanHpPercent: ResolveStatic(schema, GameSchema.AutoTrainMenu.ClassName, GameSchema.AutoTrainMenu.StaticPeanHpPercent),
            PeanMpPercent: ResolveStatic(schema, GameSchema.AutoTrainMenu.ClassName, GameSchema.AutoTrainMenu.StaticPeanMpPercent),
            IsCheckAllMob: ResolveStatic(schema, GameSchema.AutoTrainMenu.ClassName, GameSchema.AutoTrainMenu.StaticIsCheckAllMob),
            IsCheckedMobName: ResolveStatic(schema, GameSchema.AutoTrainMenu.ClassName, GameSchema.AutoTrainMenu.StaticIsCheckedMobName),
            ListMobNames: ResolveStatic(schema, GameSchema.AutoTrainMenu.ClassName, GameSchema.AutoTrainMenu.StaticListMobNames),
            ListSkills: ResolveStatic(schema, GameSchema.AutoTrainMenu.ClassName, GameSchema.AutoTrainMenu.StaticListSkills),
            IsCheckAttackByHp: ResolveStatic(schema, GameSchema.AutoTrainMenu.ClassName, GameSchema.AutoTrainMenu.StaticIsCheckAttackByHp),
            HpQuaiOn: ResolveStatic(schema, GameSchema.AutoTrainMenu.ClassName, GameSchema.AutoTrainMenu.StaticHpQuaiOn),
            HpQuaiDuoi: ResolveStatic(schema, GameSchema.AutoTrainMenu.ClassName, GameSchema.AutoTrainMenu.StaticHpQuaiDuoi),
            IsCheckItem: ResolveStatic(schema, GameSchema.AutoTrainMenu.ClassName, GameSchema.AutoTrainMenu.StaticIsCheckItem),
            IsCheckPick: ResolveStatic(schema, GameSchema.AutoTrainMenu.ClassName, GameSchema.AutoTrainMenu.StaticIsCheckPick),
            IsCheckBua: ResolveStatic(schema, GameSchema.AutoTrainMenu.ClassName, GameSchema.AutoTrainMenu.StaticIsCheckBua),
            MuaBuaKills: ResolveStatic(schema, GameSchema.AutoTrainMenu.ClassName, GameSchema.AutoTrainMenu.StaticMuaBuaKills),
            LastTimeUseItem: ResolveStatic(schema, GameSchema.AutoTrainMenu.ClassName, GameSchema.AutoTrainMenu.StaticLastTimeUseItem),
            IsNhat: ResolveStatic(schema, GameSchema.AutoTrainMenu.ClassName, GameSchema.AutoTrainMenu.StaticIsNhat),
            StrIdNhat: ResolveStatic(schema, GameSchema.AutoTrainMenu.ClassName, GameSchema.AutoTrainMenu.StaticStrIdNhat),
            StrIdLoaiTru: ResolveStatic(schema, GameSchema.AutoTrainMenu.ClassName, GameSchema.AutoTrainMenu.StaticStrIdLoaiTru),
            StrIdVutBo: ResolveStatic(schema, GameSchema.AutoTrainMenu.ClassName, GameSchema.AutoTrainMenu.StaticStrIdVutBo),
            TfHpOn: ResolveInstance(schema, GameSchema.AutoTrainMenu.ClassName, GameSchema.AutoTrainMenu.FieldTfHpOn),
            TfHpDuoi: ResolveInstance(schema, GameSchema.AutoTrainMenu.ClassName, GameSchema.AutoTrainMenu.FieldTfHpDuoi),
            TfHpPean: ResolveInstance(schema, GameSchema.AutoTrainMenu.ClassName, GameSchema.AutoTrainMenu.FieldTfHpPean),
            TfMpPean: ResolveInstance(schema, GameSchema.AutoTrainMenu.ClassName, GameSchema.AutoTrainMenu.FieldTfMpPean),
            TfIdNhat: ResolveInstance(schema, GameSchema.AutoTrainMenu.ClassName, GameSchema.AutoTrainMenu.FieldTfIdNhat),
            TfIdLoaiTru: ResolveInstance(schema, GameSchema.AutoTrainMenu.ClassName, GameSchema.AutoTrainMenu.FieldTfIdLoaiTru),
            TfIdVutBo: ResolveInstance(schema, GameSchema.AutoTrainMenu.ClassName, GameSchema.AutoTrainMenu.FieldTfIdVutBo),
            TfBuaKills: ResolveInstance(schema, GameSchema.AutoTrainMenu.ClassName, GameSchema.AutoTrainMenu.FieldTfBuaKills)
        );

        MResources = new MResourcesLayout(
            AutoTrainTabNames: ResolveStatic(schema, GameSchema.mResources.ClassName, GameSchema.mResources.StaticAutoTrainTabNames),
            AutoTrainTabNames2: ResolveStatic(schema, GameSchema.mResources.ClassName, GameSchema.mResources.StaticAutoTrainTabNames2),
            AutoPickNames: ResolveStatic(schema, GameSchema.mResources.ClassName, GameSchema.mResources.StaticAutoPickNames)
        );

        MGraphics = new MGraphicsLayout(
            ZoomLevel: ResolveStatic(schema, GameSchema.mGraphics.ClassName, GameSchema.mGraphics.StaticZoomLevel)
        );

        ScaleGUI = new ScaleGUILayout(
            ScaleScreen: ResolveStatic(schema, GameSchema.ScaleGUI.ClassName, GameSchema.ScaleGUI.StaticScaleScreen),
            Width: ResolveStatic(schema, GameSchema.ScaleGUI.ClassName, GameSchema.ScaleGUI.StaticWidth),
            Height: ResolveStatic(schema, GameSchema.ScaleGUI.ClassName, GameSchema.ScaleGUI.StaticHeight)
        );

        MotherCanvas = new MotherCanvasLayout(
            Instance: ResolveStatic(schema, GameSchema.MotherCanvas.ClassName, GameSchema.MotherCanvas.StaticInstance),
            ZoomLevel: ResolveInstance(schema, GameSchema.MotherCanvas.ClassName, GameSchema.MotherCanvas.FieldZoomLevel)
        );

        CharRva = schema.GetTypeInfoRva("Char");
        TileMapRva = schema.GetTypeInfoRva("TileMap");
        GameScrRva = schema.GetTypeInfoRva("GameScr");
        GameCanvasRva = schema.GetTypeInfoRva("GameCanvas");
        GameMidletRva = schema.GetTypeInfoRva("GameMidlet");
        MainRva = schema.GetTypeInfoRva("Main");
        RmsRva = schema.GetTypeInfoRva("Rms");
        ServiceRva = schema.GetTypeInfoRva("Service");
        AutoTrainMenuRva = schema.GetTypeInfoRva("AutoTrainMenu");
        MResourcesRva = schema.GetTypeInfoRva(GameSchema.mResources.ClassName);
        MGraphicsRva = schema.GetTypeInfoRva(GameSchema.mGraphics.ClassName);
    }

    private static int ResolveInstance(GameVersionSchema schema, string className, string fieldName, int fallback = -1)
    {
        int offset = schema.GetInstanceFieldOffset(className, fieldName);
        return offset >= 0 ? offset : fallback;
    }

    private static int ResolveStatic(GameVersionSchema schema, string className, string fieldName, int fallback = -1)
    {
        int offset = schema.GetStaticFieldOffset(className, fieldName);
        return offset >= 0 ? offset : fallback;
    }
}
