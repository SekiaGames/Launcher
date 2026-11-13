namespace OpenWC3;

// 事件类型 (对标 Warsmash JassGameEventsWar3): 流程各阶段派发
public enum GameEvent
{
    GameVictory,
    GameEndLevel,
    GameLoaded,
    GameSave,
    GameStateChanged,   // 游戏级状态(TimeOfDay/DivineIntervention/Disconnected)越过阈值
    GameTrackableHit,   // 可追踪物被命中 (EVENT_GAME_TRACKABLE_HIT)
    GameTrackableTrack, // 可追踪物被路过 (EVENT_GAME_TRACKABLE_TRACK)
    GameShowSkill,      // 显示子菜单技能 (EVENT_GAME_SHOW_SKILL)
    GameBuildSubmenu,   // 构建子菜单 (EVENT_GAME_BUILD_SUBMENU)
    GameTournamentFinishSoon,   // 锦标赛即将结束
    GameTournamentFinishNow,    // 锦标赛结束
    GameCustomUiFrame,  // 自定义 UI 框架事件
    PlayerMouseDown,    // 玩家鼠标按下
    PlayerMouseUp,      // 玩家鼠标抬起
    PlayerMouseMove,    // 玩家鼠标移动
    PlayerSyncData,     // 多人同步数据
    PlayerKey,          // 键盘事件(通用)
    PlayerKeyDown,      // 键盘按下
    PlayerKeyUp,        // 键盘抬起
    PlayerArrowLeftDown, PlayerArrowLeftUp,
    PlayerArrowRightDown, PlayerArrowRightUp,
    PlayerArrowDownDown, PlayerArrowDownUp,
    PlayerArrowUpDown, PlayerArrowUpUp,
    PlayerStateChanged,
    PlayerAllianceChanged,
    PlayerDefeat,
    PlayerVictory,
    PlayerLeave,
    PlayerChat,
    PlayerEndCinematic,
    UnitStateChanged,
    UnitAcquiredTarget, // 单位攻击行为获得/切换目标 (EVENT_UNIT_ACQUIRED_TARGET)
    OrderIssued,    // 下达订单 (EVENT_UNIT_ISSUED_ORDER)
    UnitAttacked,   // 被攻击/挥击命中前 (EVENT_UNIT_ATTACKED)
    UnitDamaging,   // 伤害结算前 — handler 可改伤害/取消 (EVENT_UNIT_DAMAGING)
    UnitDamaged,    // 伤害结算后 (EVENT_UNIT_DAMAGED)
    DestructableDamaged, // 可破坏物(树/岩石/门)被攻击命中
    SpellCast,      // 施法开始 (EVENT_UNIT_SPELL_CAST)
    SpellChannel,   // 吟唱 (EVENT_UNIT_SPELL_CHANNEL)
    SpellEffect,    // 效果生效 (EVENT_UNIT_SPELL_EFFECT)
    SpellEndcast,   // 施法结束 (EVENT_UNIT_SPELL_ENDCAST)
    SpellFinish,    // 完成 (EVENT_UNIT_SPELL_FINISH)
    UnitDies,       // 死亡 (EVENT_UNIT_DEATH)
    UnitDecay,      // 尸体开始腐化(英雄复活窗口结束/普通单位变骸骨) (EVENT_UNIT_DECAY)
    UnitDetected,   // 单位从战争迷雾中显现(玩家重新获得视野) (EVENT_UNIT_DETECTED)
    UnitHidden,     // 单位消失进迷雾(玩家失去视野) (EVENT_UNIT_HIDDEN)
    UnitRevived,    // 英雄复活 (对标 heroReviveEvent)
    HeroRevivable,  // 英雄尸体进入可被复活窗口(祭坛/复活点可选) (EVENT_PLAYER_HERO_REVIVABLE)
    HeroReviveStart,    // 祭坛开始复活英雄(计时开始) (EVENT_PLAYER_HERO_REVIVE_START)
    HeroReviveCancel,   // 复活被取消(祭坛被打掉/玩家取消) (EVENT_PLAYER_HERO_REVIVE_CANCEL)
    UnitSelected,
    UnitDeselected,
    UnitRescued,
    UnitConstructStart,
    UnitConstructCancel,
    UnitConstructFinish,
    UnitUpgradeStart,
    UnitUpgradeCancel,
    UnitUpgradeFinish,
    UnitTrainStart,
    UnitTrainCancel,
    UnitTrainFinish,
    UnitResearchStart,
    UnitResearchCancel,
    UnitResearchFinish,
    UnitLoaded,
    UnitChangedOwner,
    UnitSummoned,
    HeroLevel,
    HeroSkill,
    UnitRemoved,    // 彻底移除/数据清理 (EVENT_WIDGET_DEATH 之后)
    ItemPickedUp,
    ItemDropped,
    ItemUsed,
    UnitSold,
    ItemSold,
    ItemPawned,
    UnitStackItem, // 物品在背包中堆叠 (EVENT_UNIT_STACK_ITEM)
    UnitHarvestLumber, // 工人砍树(伐木命中)
    // Dedicated spatial registrations carry these contexts directly to JASS.
    GameEnterRegion,
    GameLeaveRegion,
    UnitTargetInRange,
    // 技能效果音效(纯表现)。原版 CAbility 在效果生效时播 AbilityData 的
    // Effectsound 字段(经 UI\SoundInfo\AbilitySounds.slk 映射到 wav), 与 JASS 的
    // EVENT_UNIT_SPELL_EFFECT 是两条独立通道。
    // 本项目原先只在 CastOrder -> CompleteCast 路径上借 SpellEffect 顺手播, 走
    // IAbilityOrderBehavior 的技能(Amic 战斗号召等)不派发 SpellEffect, 其
    // Effectsound 因此形同虚设。本事件只由表现层(音频)消费, 不映射任何
    // War3EventId, 地图脚本收不到。
    AbilityEffectSound,
}
