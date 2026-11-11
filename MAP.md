# Launcher ↔ 客户端文件对照

Launcher 是客户端的「Godot + 对战」删减版：**每个文件都能在 `Open-Warcraft3_Wiki` 里找到对应的那一份**，
只保留对战地图需要的东西，去掉 JASS / 战役 / 编辑器 / 存档 / 联机 / 安卓等分支。

原则：

- 一个文件对应客户端一个文件，命名与目录尽量同名，改代码时两边可以并排看
- 单个文件超过 600 行就继续按客户端的 partial 拆法拆开（`War3GameScene/*`、`World.*` 都是客户端本来的组织方式）
- 客户端按 `L0_Foundation → L1_Adapter → L2_Core(Src) → L3_Simulation → L4_Composition` 分层，Launcher 侧对应关系见下方分组

## 场景层（客户端 `GameCore/L4_Composition/Game/Scenes/War3GameScene`）

| Launcher | 客户端 | 职责 |
|---|---|---|
| `src/Scenes/War3GameScene/Lifecycle.cs` | `War3GameScene/Lifecycle.cs` | 聚合根：宿主字段、装配流程、帧循环 |
| `src/Scenes/War3GameScene/Lobby.cs` | `Simulation/Configuration/LobbyConfiguration.cs`、`MapLobbySettings.cs` | 座位、种族、队伍、同盟 |
| `src/Scenes/War3GameScene/Units.cs` | `War3GameScene/Units.cs` | 地图单位与 melee 开局单位生成、清场 |
| `src/Scenes/War3GameScene/Input.cs` | `War3GameScene/Input.cs` | 选择、框选、编队、键鼠 |
| `src/Scenes/War3GameScene/Rally.cs` | `War3GameScene/Rally.cs` | 右键下达指令、集结点、拾取 |
| `src/Scenes/War3GameScene/Ui.cs` | `War3GameScene/Ui.cs`、`Game/UI/UnitInfoPanelFrame.cs` | 顶栏、信息面板、命令卡、鼠标提示 |
| `src/Scenes/War3GameScene/Presentation.cs` | `War3GameScene/Presentation.cs`、`Game/Audio` | 投射物、语音、命中音、完成音 |
| `src/Scenes/War3GameScene/CameraFollowTrack.cs` | `War3GameScene/CameraFollowTrack.cs`、`CameraPanUnlock.cs` | 相机档位、边界、取景 |
| `src/Scenes/War3GameScene/Terrain.cs` | `War3GameScene/Terrain.cs` | 地图坐标 ↔ 世界坐标 |
| `src/Scenes/War3GameScene/Environment.cs` | `Game/Environment`、`War3GameScene/Animation.cs` | 昼夜时钟、视距、迷雾推进 |
| `src/Scenes/War3GameScene/Production.cs` | `Src/Components/OrderQueue.cs`、`Simulation/Configuration` | 训练/研究/升级/建造订单与施工 |
| `src/Scenes/War3GameScene/Melee.cs` | `Src/Skills/GameplayConstants.cs` + `Scripts/Blizzard.j` 的 `MeleeInitVictoryDefeat` | 胜负判定、阵亡处理 |
| `src/Scenes/War3GameScene/Ai.cs` | `GameCore/L3_Simulation/Simulation/AI` | 电脑内务与进攻波次 |
| `src/Scenes/War3GameScene/Cheats.cs` | `War3GameScene/Cheats.cs`、`Diagnostics` | `--xxx-probe` 自测脚本 |

## 仿真层（客户端 `Src/` = L2_Core）

| Launcher | 客户端 | 职责 |
|---|---|---|
| `src/Units/Unit.cs` | `Src/Units/Unit.cs`、`Unit.TypeFields.cs` | 单位数据：位置、目标、指令、采集、建筑、英雄属性 |
| `src/Units/Unit.Lifecycle.cs` | `Src/Units/Unit.Lifecycle.cs`、`Components/Death.cs` | 死亡、尸体、复活上场 |
| `src/Components/State.cs` | `Src/Components/State.cs` | 计数式状态修饰（眩晕/定身/虚无/无敌/魔免…） |
| `src/World/World.cs` | `Src/Core/World.cs` | 聚合根：地形/寻路/单位集合/事件/帧推进 |
| `src/World/World.DamageMembers.cs` | `Src/Core/World.DamageMembers.cs` | 攻击执行、伤害结算、死亡 |
| `src/World/World.Targeting.cs` | `Src/Core/World.Targeting.cs` | 索敌、目标掩码、野怪守卫链 |
| `src/World/World.MovementEvents.cs` | `Src/Core/World.MovementEvents.cs` | 寻路、移动、朝向 |
| `src/World/World.MechanismMembers.cs` | `Src/Core/World.MechanismMembers.cs` | 采金、伐木、施工推进 |
| `src/World/World.UnitLookup.cs` | `Src/Core/World.UnitLookup.cs` | 最近主基地、最近树 |
| `src/World/World.RelationMembers.cs` | `Src/Core/World.RelationMembers.cs` | 同盟关系 |
| `src/World/World.Rally.cs` | `Src/Core/World.Rally.cs` | 下达采集/伐木/建造/停止 |
| `src/World/World.HeroSelectionOrder.cs` | `Src/Core/World.HeroSelectionOrder.cs` | 英雄经验与升级 |
| `src/World/World.HeroRevive.cs` | `Src/Skills/GameplayConstants.cs`、`Jass` 的复活结算 | 祭坛复活排队、赏金 |
| `src/World/MapWorldBuilder.cs` | `Src/Core/TerrainGrid.cs`、`Game/Rendering` | 地形、悬崖、水面、装饰物构建 |
| `src/World/FogOfWar.cs` | `Src/Core/FogOfWar.cs`、`Units/Unit.FogVision.cs` | 视野格、黑区、雾区 |
| `src/World/TreeState.cs` | `Src/Core/Pathing` | 可砍伐的树 |
| `src/Core/Combat/AttackType.cs` | `Src/Core/AttackType.cs` | 攻击类型 |
| `src/Core/Combat/DefenseType.cs` | `Src/Core/DefenseType.cs` | 护甲类型 |
| `src/Core/Combat/DamageType.cs` | `Src/Core/DamageType.cs` | 伤害类型 |
| `src/Core/Combat/DamageInfo.cs` | `Src/Core/DamageData.cs` | 一次伤害的上下文 |
| `src/Core/Combat/TargetFlags.cs` | `Src/Core/TargetFlags.cs` | 目标类别位 |
| `src/Core/Combat/CombatCodes.cs` | `Src/Core/NativeTargetMask.cs`、`TargetRules.cs` | 类型/掩码解析与显示名 |

## 配置与数据（客户端 `L3_Simulation/Simulation/Configuration`、`Src/Configuration`）

| Launcher | 客户端 | 职责 |
|---|---|---|
| `src/Simulation/Configuration/War3GameplayConfig.cs` | `Src/Configuration/War3GameplayConfig.cs` | 伤害矩阵、护甲系数、复活常量 |
| `src/Simulation/Configuration/MiscRules.cs` | 同上（Misc 段） | `Units\MiscGame` 的通用读取 |
| `src/Simulation/Configuration/HeroRules.cs` | 同上（英雄段） | 经验表、属性派生 |
| `src/Simulation/Configuration/MeleeRules.cs` | `Scripts/Blizzard.j` 的 melee 模板 | 开局资源、清场半径、起始单位 |
| `src/Simulation/Configuration/TechTree.cs` | `Simulation/Configuration/Runtime*` | `*UnitFunc`/`*UpgradeFunc` 数据层 |
| `src/Simulation/Configuration/UnitCatalog.cs` | `Simulation/Configuration/UnitFactory.cs` | 单位类型组装 |
| `src/Simulation/Configuration/War3Tables.cs` | `L1_Adapter/Formats`（SLK 读取） | Tables JSON → SlkTable |
| `src/Simulation/Configuration/FogRules.cs` | 同上（`UI\MiscData.txt` 的 `[FogOfWar]`） | 迷雾颜色 |

## 渲染与表现（客户端 `GameCore/L4_Composition/Game`）

| Launcher | 客户端 | 职责 |
|---|---|---|
| `src/Assets/Assets.cs`、`War3Assets.cs`、`PathResolve.cs` | `Game/Assets` | pck 挂载、资源路径解析 |
| `src/Assets/ModelLibrary.cs`、`UnitMeshLibrary.cs`、`DoodadMeshLibrary.cs` | `Game/SceneGraph`、`Game/Assets` | 模型实例化与缓存 |
| `src/Rendering/GroundTextureArray.cs`、`TerrainTileSet.cs`、`WaterTileSet.cs` | `Game/Rendering` | 地表贴图数组、水面 |
| `src/Rendering/CliffMeshFactory.cs`、`CliffTextureSet.cs`、`LayerAtlas.cs` | `Game/Rendering` | 悬崖烘焙、贴图图集 |
| `src/View/UnitView.cs` | `Game/SceneGraph` | 单位渲染节点、动画、血条、选中圈 |
| `src/Camera/War3CameraRig.cs` | `Game/Camera` | War3 相机 |
| `src/Ui/Hud.cs`、`Minimap.cs` | `Game/UI` | HUD、小地图 |
| `src/Audio/SoundBank.cs`、`WorldAudio.cs`、`MusicPlayer.cs` | `Game/Audio` | 音效表、3D 播放池、音乐 |

## 格式与地图（客户端 `L0_Foundation`、`L1_Adapter`、`Simulation/Maps`）

| Launcher | 客户端 | 职责 |
|---|---|---|
| `src/Core/Formats/Mpq/*` | `L1_Adapter/Formats`（War3Net.IO.Mpq） | MPQ 读写 |
| `src/Core/Formats/Slk/SlkTable.cs` | `L1_Adapter/Formats` | SLK 解析 |
| `src/Core/Formats/W3x/*` | `L1_Adapter/Formats` | w3e / w3i / w3o / wpm / wts / w3x 对象数据 |
| `src/Core/Map/MapDocument.cs` | `Simulation/Maps`、`Src/Resources` | 一份地图的完整数据 |
| `src/Map/MapSource.cs`、`GmapPackage.cs`、`GmapImporter.cs` | `Simulation/Maps/War3Map*.cs` | 地图读取与 gmap 打包 |
| `src/Map/PathGrid.cs` | `Src/Core/Pathing`（245 文件，Launcher 只留 A* 骨架） | 寻路网格 |
| `src/Map/War3Strings.cs` | `Simulation/Jass`（`GetLocalizedString`） | `TRIGSTR` 文本 |
| `src/MapBrowser/MapBrowser.cs` | `L2_Core/MapBrowser` | 地图列表 |

## 自测与启动器

| Launcher | 客户端 | 说明 |
|---|---|---|
| `src/Diagnostics/CombatProbe.cs` | `Game/Diagnostics/*` | `--combat-probe` / `--state-probe` |
| `src/Diagnostics/WorldTest.cs` | `Game/Diagnostics/*` | `--sim-test` 无渲染单挑 |
| `src/App/LauncherApp.cs` | `Game/Program.cs` + `Game/Hosting` | 启动器自有：CLI 解析、pck 挂载、进图/回列表 |

## 明确不做的部分（客户端有、Launcher 没有）

- JASS 虚拟机与触发器（`Simulation/Jass`）、地图脚本
- 战役、存档读档、录像（`World.Recording.cs`）
- 联机与大厅、安卓桥接
- 物品系统、技能系统（下一步才做，做完会按 `Src/Items`、`Src/Skills` 一样建文件）
- 编辑器专用的物体数据编辑
