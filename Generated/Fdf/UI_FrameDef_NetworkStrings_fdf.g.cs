namespace OpenWC3.Game.Generated.Fdf;

internal static class UI_FrameDef_NetworkStrings_fdf
{
    internal const string Text =
@"/*
 *  Network Strings
 *  ---------------
 *
 *  This file contains all strings that can be displayed in response to network activity.
 *
 */

StringList {

    // --- network error message (map to error id)
    ERROR_ID_INVALIDPARAMS              ""提供的参数无效。"",
    ERROR_ID_AUTOCANCEL                 ""Battle.net已经自动取消了你的请求。"",
    ERROR_ID_BADPROVIDER                ""操作失败，因为当前的网络是无效的。"",
    ERROR_ID_NOTINITIALIZED             ""操作失败，因为当前的网络并没有被初始化。"",
    ERROR_ID_ALREADYINITIALIZED         ""操作失败，因为网络已经被其他程序初始化了。"",
    ERROR_ID_GAMEPORTINUSE              ""其他程序在使用游戏所需要的端口。请退出其他程序之后再进行尝试。"",
    ERROR_ID_GAMENOTFOUND               ""寻找游戏失败。"",
    ERROR_ID_GAMEUNJOINABLE             ""指定的游戏不能加入。|N|N游戏可能不存在，或是已经开始了。"",
    ERROR_ID_GAMEFULL                   ""指定的游戏人数已满，或者创建者已经关闭了所有剩余的位置。"",
    ERROR_ID_GAMECLOSED                 ""指定的游戏已经开始了。"",
    ERROR_ID_CANTCONNECT                ""不能连接上。"",
    ERROR_ID_DISCONNECT                 ""你和Battle.net的连接已经中断。"",
    ERROR_ID_NOTCONNECTED               ""操作失败，因为你并没有连接上。"",
    ERROR_ID_NOTIMPLEMENTED             ""这个功能不能开启。"",
    ERROR_ID_VERSION_BAD                ""不能验证游戏版本。请重新连接到Battle.net上，或者亲自去下载最近的补丁程序。"",
    ERROR_ID_CDKEY_INVALID              ""这个CD Key是无效的。"",
    ERROR_ID_CDKEY_INUSE                ""这个CD Key正在被%s使用。(%d)"",
    ERROR_ID_CDKEY_DISABLED             ""你所提供的CD Key是不可用的。(%d)"",
    ERROR_ID_CDKEY_LANGUAGE             ""你所连接的版本需要特定语言版本之Windows。(%d)"",
    ERROR_ID_CDKEY_MISMATCH             ""你所提供的CD Key是不可用的。"",
    ERROR_ID_BADSERVER                  ""操作失败因为指定的服务器是无效的。"",
    ERROR_ID_UNKNOWNACCOUNT             ""这个账号并不存在。|n|n魔兽争霸III需要一个新的Battle.net账号。|n|n请选择 '创建新账号' 来创建一对新的名字和密码。"",
    ERROR_ID_UNSUPPORTEDACCOUNT         ""这个账号得不到魔兽争霸III的确认。|n|n请选择 '创建新账号' 来创建一个新的用户名和密码。"",
    ERROR_ID_BADPASSWORD                ""你所提供的密码是不正确的，请重试。"",
    ERROR_ID_BADCDKEY                   ""这个CD Key是无效的。"",
    ERROR_ID_NAMEUSED                   ""指定的账号名字已经存在。请选择其他的。"",
    ERROR_ID_NAMEBLANK                  ""没有指定账号名字，请输入你的账号名字。"",
    ERROR_ID_NAMEBADCHARS               ""指定的账号名字中包含一些错误字符。"",
    ERROR_ID_NAMEBADWORD                ""指定的账号名字中包含一些错误的文字。"",
    ERROR_ID_NEEDALPHACHARS             ""指定的账号名字中没有包含足够的字母字符。"",
    ERROR_ID_ADJACENTPUNCT              ""指定的账号名字中包含太多邻近的标点符号。"",
    ERROR_ID_TOOMANYPUNCT               ""指定的账号名字中包含太多的标点符号。"",
    ERROR_ID_NOTLOGGEDON                ""指定的操作不能完成，因为你没有登陆。"",
    ERROR_ID_NOTINCHAT                  ""操作失败，因为你并没有处于聊天的状态之中。"",
    ERROR_ID_UPGRADEFAILED              ""在升级的时候发生了错误。"",
    ERROR_ID_FLOODDETECTED              ""发生了网络溢出现象。|n|n你已经从Battle.net上中断了连接。"",
    ERROR_ID_GAMETYPEDISABLED           ""指定的游戏类型已经是无效的了。"",
    ERROR_ID_GAMECREATEFAILED           ""游戏创建失败。"",
    ERROR_ID_REQUESTDENIED              ""所要求的操作被拒绝了。"",
    ERROR_ID_ANONFINDFAILED             ""Battle.net并不能找到你所指定的游戏。请耐心地等待几分钟，然后再试。"",
    ERROR_ID_ANONFINDCANCELLED          ""找到的匿名游戏已经被取消了。"",
    ERROR_ID_ANONFINDBADVERSION         ""游戏的种类在Battle.net上已经得到更新。|n|n请重新设置你的游戏偏好属性并重新点击'开始游戏'按钮。 "",
    ERROR_ID_ANONFINDNOOPPONENT         ""你的对手并没有出现在这场游戏中。"",
    ERROR_ID_DOWNLOADFAILED             ""在试图从Battle.net上下载一个文件的时候发生了错误。"",
    ERROR_ID_DOWNLOADFAILED_BADSIZE     ""你的硬盘空间不够，以至于不能从Battle.net上下载一个文件。请释放一定的硬盘空间然后再试。"",
    ERROR_ID_DOWNLOADFAILED_WRITE       ""在下载一个文件到你的硬盘上的时候发生了硬盘的读写错误。可能你需要释放一定的硬盘空间。或者请你选择另一个硬盘然后再试。"",
    ERROR_ID_FILENOTFOUND               ""在Battle.net上并没有找到指定的文件。"", 
    ERROR_ID_ANONFINDQUOTAFILLED        ""对于 '%s' 的游戏类型，你已经达到了最大的游戏数目。"",
    ERROR_ID_ANONFINDCDKEYDISABLED      ""你的 CD key 在排名赛中不可用了。"",
    ERROR_ID_ANONFINDSAMEACCOUNT        ""不能搜索对手。你队伍中有两个或者两个以上的人使用了相同的名字。"",

    // --- miscellaneous error messages
    NETERROR_NOUSERNAME                 ""请输入一个用户名。"",
    NETERROR_NOPASSWORD                 ""请输入密码。"",
    NETERROR_LOGINNOUSERNAME            ""请输入你的用户名。"",
    NETERROR_LOGINNOPASSWORD            ""请输入你的密码。"",
    NETERROR_REPEATPASSWORD             ""请重复你的密码。"",
    NETERROR_PASSWORDMISMATCH           ""输入的密码和重复的密码并不符合。"",
    NETERROR_USERNAMEPASSWORD           ""你的密码必须符合你的用户名。"",
    NETERROR_USERNAMETOOSHORT           ""你的账号名字必须有3个字符的长度。"",
    NETERROR_PASSWORDTOOSHORT           ""你的密码必须有3个字符的长度。"",
    NETERROR_NEWPASSWORDTOOSHORT        ""你的新密码必须有3个字符的长度。"",
    NETERROR_NOCURRENTPASSWORD          ""请输入你当前的密码。"",
    NETERROR_NONEWPASSWORD              ""请输入新的密码。"",
    NETERROR_REPEATNEWPASSWORD          ""请重复你的密码。"",
    NETERROR_NEWPASSWORDMISMATCH        ""新密码和重复的密码并不符合。"",
    NETERROR_NEWUSERNAMEPASSWORD        ""你的新密码没有符合你的账号。"",
    NETERROR_DEFAULTERROR               ""在处理请求的时候发生了异常错误。"",
    NETERROR_CORRUPTSAVE                ""在载入游戏的过程中发生了错误。|n|n存档或者是不可用或者是已经被损坏。"",
    NETERROR_NOGAMESPECIFIED            ""你必须指定一个游戏以便加入。"",
    NETERROR_NOMAPSELECTED              ""你必须选择一个地图。"",
    NETERROR_NOREPLAYSELECTED           ""你必须选择一个录象文件。"",
    NETERROR_NOSAVEDGAMESELECTED        ""你必须选择一个存档。"",
    NETERROR_MAPLOADERROR               ""在试图载入指定的地图的时候发生了错误。|n|n地图找不到或是损毁。"",
    NETERROR_NOGAMENAMESPECIFIED        ""你必须指定一个用户名以创建一个游戏。"",
    NETERROR_EMPTYGAMENAMESPECIFIED     ""游戏名字不能只包含空格。|n|n请输入一个游戏名字，其中必须要包括一个以上的字符。"",
    NETERROR_CREATEGAMEFAILED           ""指定的游戏并不能被顺利创建。|n|n或许已经有相同的游戏名存在，或者你所起的名字服务器不能接受。"",
    NETERROR_JOINGAMEFAILED             ""不能加入指定的游戏。|n|n请重新再试，或者选择一个其他的游戏。"",
    NETERROR_NOREALMSELECTED            ""请选择一个网关。"",
    NETERROR_COULDNOTCONNECT            ""不能连入网络。"",
    NETERROR_CUSTOMGAMEDISABLED         ""自定义游戏当前是不可用的。|n|n请选择另外一种游戏类型之后再试。"",
    NETERROR_STANDARDGAMEDISABLED       ""标准的游戏类型当前是不可用的。|n|n请选择另外一种游戏类型之后再试。"",
    NETERROR_JOINGAMENOTFOUND           ""不能发现你所试图进入的游戏。|n|n你或许输入了一个错误的游戏名字，或者游戏的创建者已经取消了该游戏。"",
    NETERROR_CANTCONNECTBNET            ""不能连接到Battle.net。|n|n你或许正在尝试连接一个无效的Battle.net服务器。如果你使用的是调制解调器，那么可能在你连接Battle.net之前，你必须手动地方式将电脑连接到因特网上。"",
	NETERROR_CANTLOADREPLAYFILE         ""不能载入指定的录象文件。"",
	NETERROR_CANTLOADREPLAYDATA         ""录象内的数据不能正常地被载入。"",
    NETERROR_CANTFINDREPLAYMAP          ""不能找到和这个录象文件相关的地图。|n|n请确保录象文件所使用的地图文件「%s」存在于你的电脑中并重试一遍。"",
	NETERROR_CANTLOADREPLAYMAP          ""录象内包含的地图文件不能正常地载入。|n|n你电脑上的地图文件 '%s' 不是已经被损坏或者和录像所使用的地图有所不同。"",
	NETERROR_BADREPLAYVERSION           ""这个录象是在另外一个版本的魔兽争霸III中所录制的。"",
    NETERROR_BADREPLAYPROGRAM_CLASSIC   ""为了观看这个录象你必须运行魔兽争霸III的经典版。"",
    NETERROR_BADREPLAYPROGRAM_EXPANSION ""为了观看这个录象你必须运行魔兽争霸III的资料片版。"",
    NETERROR_NOCHANNELNAME              ""请输入一个欲加入的频道名字。"",
    NETERROR_TEAMMUSTBEFULL             ""在你的队友接受你的请求并加入你的队伍之前，你是不能进行一次游戏搜索的。"",
    NETERROR_NOGAMETYPESELECTED         ""请选择一种游戏类型。"",
    NETERROR_JOINTEAMGAMENOTFOUND       ""你不能加入该组队模式游戏。|n|n这可能是因为该游戏的创建者已经取消了该游戏，或者另外一支队伍已经抢在你的前面加入了该游戏。"",
    NETERROR_JOINTEAMCANTCONNECT        ""不能加入队伍。|N|N可能是因为队伍的创建者的电脑对于网络的连接有一些限制条件。|N|N另外，也有可能是由于你开着防火墙，所以你无法发送对于邀请的回答。请你确保你的防火墙能允许游戏端口的数据传输。"",
    NETERROR_NOPLAYERNAMESPECIFIED      ""请输入一个玩家的名字。"",
    NETERROR_MAPAUTHFAILED              ""不能通过该地图。"",
    NETERROR_MAPFILEBADSIZE             ""该该图太大了。请选择另外一张地图。"",
    NETERROR_MAPFILENOTFOUND            ""无法找到该地图文件。"",
    NETERROR_MAPFILEREAD                ""在读取该地图文件的时候发生了错误。"",
    NETERROR_MAPFILEINCOMPLETE          ""在下载该地图文件的时候发生了错误。"",
    NETERROR_MAPFILEWRITE               ""在写入地图文件的时候发生了错误。"",
    NETERROR_MAPFILENAMEINVALID         ""地图文件名太长或者包含了错误字符。"",
    NETERROR_TEAMMATECOUNT              ""你需要选择至少一个队友来进行邀请。"",
    NETERROR_WOULDFLOOD                 ""你对Battle.net发送命令的频率过快了。这样可能导致你网络溢出的危险。请减慢你的速度。"",
    NETERROR_SAVEGAMENOTFOUND           ""指定的游戏需要一个特定的存档，但是该存档目前找不到。"",
    NETERROR_BADMAP                     ""很多官方的地图文件都已经丢失或者被损坏，请重新安装该游戏。"",
    NETERROR_DIFFERENTSAVEFILE          ""你的存档和创建者的存档有所不一样。|n|n这可能是因为储存游戏时你并未参与游戏，或是你的文件毁损。 "",
    NETERROR_WRONGMAPINSAVEFILE         ""在存档中所包含的地图文件同你电脑中的地图有所不一样。 "",
    NETERROR_CLANMATECOUNT              ""为了创建一个战队，你必须再挑选其他9名玩家。"",
    NETERROR_NEEDCLANNAME               ""为了创建一个战队，你必须先给它起一个响亮的名字。"",
    NETERROR_NEEDCLANTAG                ""你必须给自己的战队起一个标志。"",
    NETERROR_CLANTAGSIZE                ""战队标志的长度应该在2到4个字符之间。"",
    NETERROR_INVALIDTOURNAMENTID        ""在辨别你所进入的锦标赛的时候发生了异常错误。你的设置资料或许是过时的或者是无效的。"",
    NETERROR_CLANDISBANDTOOSOON         ""不能解散战队，刚创建不久战队要一定长的时间才能解散。"",
    NETERROR_MISSINGMMDATA              ""不能开始游戏，因为在从Battle.net上获取比赛配对资料的时候发生了错误。请待会再进行尝试。"",
    NETERROR_MISSINGMMDATAQUICK         ""由于在从Battle.net上获取比赛配对信息的时候发生了错误，所以不能开始快速游戏搜索。|N|N请进入标准游戏屏幕，使用 '开始游戏' 按钮来重新获得新的数据和设置你的偏好选项。|N|N如果还继续发生类似问题的话，那么就请待会再试。"",
    NETERROR_MISSINGMMDATASCHED         ""无法开始预定游戏搜索。因为从Battle.net获取匹配信息时发生错误。"",
    NETERROR_MISSINGMMDATAQUESTION      ""由于在Ballte.net上获取比赛配对信息的时候发生了错误，所以无法开始游戏搜索。|N|N你是否想继续尝试获取这些信息？|N|N注意，如果还是发生类似的问题，那么就请在稍等一会后再进行尝试。"",
    NETERROR_NEEDMMDATAUPDATE           ""Battle.net上的比赛配对信息已经被更新了。|N|N请进入标准游戏屏幕并点击 '开始游戏' 按钮来获取新的信息和设置新的偏好选项。"",
    NETERROR_NEEDMMDATAUPDATETEAM       ""Battle.net上的比赛配对信息已经被更新了。|N|N请重新创建队伍来获取新的信息和设置新的队伍模式偏好选项。"",
    NETERROR_UPDATEFAILEDTEAM           ""在获取新的队伍模式比赛配对信息的时候发生了错误。请待会再试。"",
    NETERROR_CLANCREATETIMEOUT          ""由于Battle.net未能收到一个或多个被邀请者的回应，建立战队失败。|N|N请确认所有被邀请者都是可加入并且是愿意加入的，然后再重试建立战队。"",
    NETERROR_NOCAMPAIGNSELECTED         ""你必须选择自定义战役。"",
    NETERROR_REQUIREDMATECOUNT          ""你必须刚好选择%d名队友才能创建团队。"",
    NETERROR_REQUIREDMATECOUNT_ONE      ""你必须刚好选择1名队友才能创建团队。"",
    NETERROR_CANTSAVE                   ""磁盘上没有足够的空间存储此游戏，请释放更多磁盘空间然后重试。"",
    NETERROR_CHANNELISRESTRICTED        ""频道 '%s' 是一个受到限制的频道，因此你不能加入。"",
    NETERROR_CHANNEISFULL               ""频道 '%s' 人数已满，因此你不能加入。"",
    NETERROR_ANONFINDTIMEOUT            ""不能开始游戏搜索。|N|N请稍候再试。"",
    NETERROR_ANONFINDTIMEOUT_TOURN      ""Battle.net不能开始游戏，因为你的一个或者多个队友还没有出现。|N|N这局游戏被放弃了。"",
    NETERROR_CANTCONNECTGAMEJOIN        ""不能联入游戏。|N|N这可能是因为网络失去连接的关系。请确保你自己还联在网络上。"",
    NETERROR_CLANCANTRANKINITIATE       ""你不能更改这个战队成员的排名，直到他的试用期期满。"",
    NETERROR_CLANCANTREMOVEINITIATE     ""直到他们的试用期期满之后，你才能删除某些战队队员。"",
    NETERROR_CLANCANTDESIGNATEINITIATE  ""直到该队员的试用期期满之后，你才可将战队所有权交给他/她。"",
    NETERROR_CLANCANTLEAVEINITIATE      ""你不能作为一个新队员来离开该战队。只有在你的试用期期满之后，你才能离开该战队。"",
    NETERROR_NOEMAIL                    ""请输入一个Email地址。"",
    NETERROR_NOOLDEMAIL                 ""请输入你的老Email地址。"",
    NETERROR_NONEWEMAIL                 ""请输入你新的Email地址。"",
    NETERROR_REPEATNEWEMAIL             ""请确认你所输入的Email地址。再输入一次。"",
    NETERROR_NEWEMAILMISMATCH           ""新输入的Email地址和重复输入的地址不符合。"",
    NETERROR_INVALIDEMAIL               ""请输入一个有效的Email地址。"",
    NETERROR_REPEATEMAIL                ""请重新输入你的Email地址以确保地址的正确性。"",
    NETERROR_EMAILMISMATCH              ""Email地址和重复输入的地址不相符合。"",
	
    // --- misc net messages
    NETMESSAGE_ACCOUNTCREATE            ""你的账号已经被创建。|n|n请点击确定按钮来连接到|n|nBattle.net上。"",
    NETMESSAGE_PASSWORDCHANGE           ""你的密码已经被改变了。"",
    NETMESSAGE_ANONFINDREQUEST          ""发出对游戏的搜索请求中..."",
    NETMESSAGE_ANONFINDINPROGRESS       ""搜索一个合适的游戏中..."",
    NETMESSAGE_ANONFINDFOUND            ""找到一个匹配你要求的游戏。"",
    NETMESSAGE_ANONFINDCANCELING        ""取消搜索中..."",
    NETMESSAGE_ANONJOINWAIT             ""等待其他的玩家中..."",
    NETMESSAGE_ANONJOINTIMEOUT          ""不能加入该游戏，因为其他玩家并没有出现。|n|n请再进行搜索。"",
    NETMESSAGE_PLAYERJOINED             ""|CFFAAAAAA%s加入了游戏。"",
    NETMESSAGE_PLAYERLEFT               ""|CFFAAAAAA%s离开了游戏。"",
    NETMESSAGE_NOPROFILE                ""对于这个账号来讲没有什么档案数据。"",
    NETMESSAGE_CLANRANKSUCCESS          ""%s的排名已经变更了。"",
    NETMESSAGE_CLANADDSUCCESS           ""%s接受了来自你的邀请，并加入了你的战队之中。"",
    NETMESSAGE_CLANREMOVESUCCESS        ""%s已经从战队中除名了。"",
    NETMESSAGE_CLANLEAVESUCCESS         ""你已经离开了战队。"",
    NETMESSAGE_CLANDESIGNATESUCCESS     ""%s现在是新的战队领袖。"",
    NETMESSAGE_CLANDISBANDED            ""这个战队现在已经被解散了。"",
    NETMESSAGE_CLANDISBANDCONFIRM       ""解散战队将把所有的成员都去除，战队的排名资料也将被销毁。值得提醒的是，这是一个不可反悔的操作。所有资料一旦删除以后都不复存在。|n|n你现在还确定想解散这个战队吗？"",
	NETMESSAGE_CLANDESIGNATECONFIRM     ""你将指定战队成员|Cfffed312%s|R成为新的战队领导者。你确定是想这么做吗？"",
    NETMESSAGE_EMAILCHANGE              ""你的Email地址已经被更改了。"",
    NETMESSAGE_PASSWORDRECOVERY         ""你对于新密码的请求已经被收到了。如果你为这个帐号注册过相应的Email地址，那么你就会收到一封Email以告诉你改如何完成密码的更改。"",
    NETMESSAGE_EMAILBIND                ""所指定的Email地址已经被注册到了你的帐号上。"",
    NETMESSAGE_CONFIRMNOEMAILBIND       ""如果没有注册Email地址，你将无法恢复自己的帐号，而且你注册Email地址的机会只有一次。|N|N你确信自己不想为帐号注册Email地址吗？"",

    // --- clan result codes
    CLAN_RESULT_TOOSOON                 ""因为你在最短的战队创建时间内已经创建了一个战队了，所以你无法在这个期间再创建一个战队。"",
    CLAN_RESULT_NAMEINUSE               ""你所选择的战队标志已经被其他战队所使用了，请选择另外一个。"",
    CLAN_RESULT_DECLINE                 ""|Cfffed312%s|R拒绝加入到你的战队中来。"",
    CLAN_RESULT_UNAVAILABLE             ""|Cfffed312%s|R无法收到你的战队邀请。|N|N要收到战队邀请，玩家必须未加入任何战队而且必须正处于Battle.net聊天室中。|N|N玩家在搜索游戏过程中也无法收到邀请。"",
    CLAN_RESULT_NOTALLOWED_NAMED        ""你不能对%s实行这个操作。"",
    CLAN_RESULT_NOTAUTHORIZED_NAMED     ""你没有什么权利来对%s实行这个操作。"",
    CLAN_RESULT_NOTALLOWED              ""你不能实行这个操作。"",
    CLAN_RESULT_NOTAUTHORIZED           ""你没有什么权利来实行这个操作。"",
    CLAN_RESULT_CLANFULL                ""由于你的战队的人数已经达到饱和状态，所以你无法邀请新的成员加入。"",
    CLAN_RESULT_NOTENOUGH               ""CLAN_RESULT_NOTENOUGH"",
    CLAN_RESULT_BADTAG                  ""你选择的战队标志不是可接受的标志，请选择其他战队标志。"",
    CLAN_RESULT_BADNAME                 ""你选择的战队名字不是可接受的名字，请选择其他战队名字。"",

    // --- tournament result codes
    TOURNAMENT_SIGNUP_RESULT_SIGNEDUP   ""你成功地加入了该锦标赛。"",
    TOURNAMENT_SIGNUP_RESULT_INELIGIBLE ""你没有什么资格来加入该锦标赛。"",
    TOURNAMENT_LEAVE_RESULT_OK          ""你已经退出了这个锦标赛。"",
    TOURNAMENT_LEAVE_RESULT_TEAMMATELEFT ""你已经被从这个锦标赛中开除了，因为你的一个队友自动退出了该锦标赛。"",
    TOURNAMENT_LEAVE_RESULT_NOTOURNAMENT ""你当前没有加入什么锦标赛。"",

    
    // --- chatdisplay colors
    CHATCOLOR_TALK_USER                 ""|CFFFED312"",
    CHATCOLOR_TALK_MESSAGE              ""|CFFFFFFFF"",
    CHATCOLOR_WHISPER_USER              ""|CFFFED312"",
    CHATCOLOR_WHISPER_MESSAGE           ""|CFF00FF00"",
    CHATCOLOR_WHISPERSENT_USER          ""|CFFFED312"",
    CHATCOLOR_WHISPERSENT_MESSAGE       ""|CFF00FF00"",
    CHATCOLOR_INFO_MESSAGE              ""|CFF80D8FF"",
    CHATCOLOR_BROADCAST_MESSAGE         ""|CFF80D8FF"",
    CHATCOLOR_EMOTE_MESSAGE             ""|CFF888888"",
    CHATCOLOR_ERROR_MESSAGE             ""|CFFFF0000"",

    // --- chatdisplay messages (map to chatevent id)
    CHATEVENT_ID_TALK                   ""%s%s：|R %s%s |R"",
        // 1:CHATCOLOR_TALK_USER
        // 2:<username>
        // 3:CHATCOLOR_TALK_MESSAGE
        // 4:<message>
    CHATEVENT_ID_WHISPER                ""%s%s 密语：|R %s%s |R"",
        // 1:CHATCOLOR_WHISPER_USER
        // 2:<username>
        // 3:CHATCOLOR_WHISPER_MESSAGE
        // 4:<message>
    CHATEVENT_ID_WHISPERSENT            ""%s你密语给 %s：|R %s%s |R"",
        // 1:CHATCOLOR_WHISPERSENT_USER
        // 2:<username>
        // 3:CHATCOLOR_WHISPERSENT_MESSAGE
        // 4:<message>
    CHATEVENT_ID_INFORMATION            ""%s%s |R"",
        // 1:CHATCOLOR_INFO_MESSAGE
        // 2:<message>
    CHATEVENT_ID_BROADCAST              ""%s%s |R"",
        // 1:CHATCOLOR_BROADCAST_MESSAGE
        // 2:<message>
    CHATEVENT_ID_EMOTE                  ""%s%s %s |R"",
        // 1:CHATCOLOR_EMOTE_MESSAGE
        // 2:<username>
        // 3:<message>
    CHATEVENT_ID_ERROR                  ""%s%s |R"",
        // 1:CHATCOLOR_ERROR_MESSAGE
        // 2:<message>
}

";
}
