# Mission 카탈로그 2 — 배선가능 큐레이션 세트 (v3)

> **무엇**: 워크플로로 572개 후보를 뽑은 뒤(→ [MISSION_Pool_v3.md](MISSION_Pool_v3.md)), **바로 배선 가능한(🟢🟡) 미션만** 골라
> **훅을 실제 코드로 검증**하고 중복을 통합해 **기존 5탭(OB/CV/AF/PR/CH)에 매핑**한 채택 후보 세트.
> 작성: 2026-07-04 · 상태: **채택 후보(큐레이션 완료)** · 탭 전략: **5탭 유지 + 매핑**

## 스키마 / 범례

- **id**: 2영문(카테고리)+4숫자. 기존 카탈로그 최대번호 다음부터 이어붙임(확정 시 재정렬 가능).
- **type**: OneTime / Increment(aN+b) / Tiered(a/b/c). 목표는 전부 int.
- **보상**: g=gold, i1~i3=item1~3.
- **연동**: 🟢 바로(이벤트/카운터 존재) · 🟡 훅필요(Report 한 줄).  ※ 🟠🔴(미구현·신규시스템 의존)는 이 문서에서 제외 — 원천 풀 참조.
- **검증**: ✓ = 훅의 클래스/메서드/이벤트가 실제 코드에 존재함을 큐레이터가 확인. ? = 행동은 가능하나 훅 시그니처 미확정(배선 전 확인 필요).
- **원천**: 아이디어가 나온 원래 풀 카테고리(ST=선톡, AG=에이전트조작, SK=AI스킬, VS=비전, WB=웹검색, MD=대화모드, VC=음성, GM=미니게임, ME=기억, HK=핫키, WM=창, SE=설정, AC=캐릭터액션 등).

> **탭 매핑**: 대화계(선톡/웹검색/대화모드/음성/미니게임/기억/알림)→**CV**, 캐릭터액션→**AF**, 도구계(에이전트/스킬/비전/창/핫키/설정)→**PR**, 메타/경제/MR→**CH**.

---

## 합계 (채택 후보)

| 코드 | 탭 | 기존 | 신규(채택) |
|------|----|-----:|-----:|
| OB | 첫걸음 | 8 | 15 |
| CV | 대화 | 9 | 34 |
| AF | 교감 | 5 | 22 |
| PR | 생활 | 8 | 35 |
| CH | 도전 | 8 | 15 |

**채택 후보 합계: 121개** (기존 38 별도, 합산 159개). 이 중 훅 검증 ✓ 121개 / 미확정 ? 0개.

---

## 미션 (탭별)

### OB — 첫걸음 · 신규 15개 (기존 8개에 이어 OB0009부터)

| id | name | type | 목표 | 보상 | 한국어 | English | 日本語 | 연동 | 검증 | 원천 |
|----|------|------|------|------|--------|---------|--------|------|------|------|
| `OB0009` | first_chat | OneTime | 1 | g40 | 아이코와 첫 대화 나누기 | Your First Chat with AICO | アイコと初めておしゃべり | 🟡 | ✓ | OB |
| `OB0010` | open_chat_balloon | OneTime | 1 | g20 | 대화창 처음 열어보기 | Open the Chat Bubble | 会話バブルを開いてみる | 🟡 | ✓ | OB |
| `OB0011` | change_character | OneTime | 1 | g30 | 캐릭터 바꿔보기 | Try a Different Character | キャラクターを変えてみる | 🟡 | ✓ | OB |
| `OB0012` | change_language | OneTime | 1 | g20 | 언어 설정 바꿔보기 | Switch the Language | 言語を切り替えてみる | 🟡 | ✓ | OB |
| `OB0013` | pat_head_first | OneTime | 1 | g20 | 머리 쓰다듬어 주기 | Pat AICO's Head | 頭をなでてあげる | 🟡 | ✓ | OB |
| `OB0014` | open_settings | OneTime | 1 | g20 | 설정 화면 열어보기 | Open the Settings | 設定画面を開いてみる | 🟡 | ✓ | OB |
| `OB0015` | wear_accessory | OneTime | 1 | g40, i1×1 | 액세서리 처음 착용하기 | Wear Your First Accessory | アクセサリーを着けてみる | 🟡 | ✓ | OB |
| `OB0016` | start_installer | OneTime | 1 | g30 | 설치 안내 시작하기 | Start the Setup Guide | インストール案内を始める | 🟡 | ✓ | OB |
| `OB0017` | explore_editions | OneTime | 1 | g30 | 에디션 설명 들어보기 | Learn About the Editions | エディションの説明を聞く | 🟡 | ✓ | OB |
| `OB0018` | install_server | OneTime | 1 | g60, i1×1 | 서버 설치 실행하기 | Run the Server Installer | サーバーのインストールを実行する | 🟢 | ✓ | OB |
| `OB0019` | installation_complete | OneTime | 1 | g80, i2×1 | 설치 완료의 순간 | Installation Complete! | インストール完了の瞬間 | 🟢 | ✓ | OB |
| `OB0020` | start_server_first | OneTime | 1 | g40 | 서버 처음 켜보기 | Start the Server for the First Time | サーバーを初めて起動する | 🟡 | ✓ | OB |
| `OB0021` | reset_settings | OneTime | 1 | g20 | 설정 초기화해보기 | Reset to Defaults | 設定を初期化してみる | 🟡 | ✓ | OB |
| `OB0022` | register_api_key | OneTime | 1 | g50, i1×1 | 나만의 API 키 등록하기 | Register Your Own API Key | 自分のAPIキーを登録する | 🟡 | ✓ | OB |
| `OB0023` | installer_already_running | OneTime | 1 | g20 | 이미 실행 중인 설치 프로그램 발견 | Installer Already Running | 実行中のインストーラーを見つける | 🟢 | ✓ | OB |

_주요 훅: ChatHandler.HandleInputSubmit(string) / HandleInputSubmitButton() / HandleInputWebSubmitButton() (ChatHandler.cs:46/80/112) 세 진입점 중 최초 전송 시 MissionList.ReportFlag. chatIdx는 AnswerBalloonManager 재생성 경로에서도 +=1 되므로(AnswerBalloonManager.cs:201) chatIdx 증가가 아닌 전송 진입점에서 신규 영구 플래그로 1회 판별할 것. · ClickHandler.HandleLeftClick() 정상 대화 분기 → ChatBalloonManager.Instance.ToggleChatBalloon() (ClickHandler.cs:163, ChatBalloonManager.cs:188) 최초 호출 시 ReportFlag. · ChangeCharListSlotController.ChangeChar() (ChangeCharListSlotController.cs:188) 및 ChangeCharCardController.ChangeChar() (ChangeCharCardController.cs:247) — 리스트/카드 양쪽 진입점 모두에 ReportFlag. · SettingManager.SetUiLanguage() (SettingManager.cs:263) 호출 시 ReportFlag. LanguageManager.SetUILanguage()를 경유하는 UI 언어 드롭다운 변경 지점. · DragHandler.PatHead() (DragHandler.cs:183) 최초 호출 시 ReportFlag. 내부에서 EmotionBalloonManager.ShowEmotionBalloon(this.gameObject, "Love") 발생(:196). · UIManager.showSettings() (UIManager.cs:536) 열림 시 ReportFlag. ToggleSettings()(:551)도 showSettings()를 경유하므로 showSettings() 한 곳에만 훅. · AccessoryManager.Equip(GameObject target, string accessoryName, string slotName=null) (AccessoryManager.cs:154) 최초 호출 시 ReportFlag. (풀 훅의 인자 순서 교정: slotName 파라미터명은 slotName) · ScenarioInstallerManager.StartInstaller() (ScenarioInstallerManager.cs:85) 호출 시 ReportFlag. 진입: ClickHandler.HandleLeftClick()에서 IsJarvisServerInstalled()==false 시 호출(ClickHandler.cs:143-145). MenuTrigger/OperatorMenuTrigger의 'Edition 튜토리얼' 메뉴에서도 동일 호출. · ScenarioInstallerManager.Scenario_I01_0_InstallServerExplain_Lite() (:300) 또는 _Full() (:335) 진입 시 ReportFlag. 트리거: OnChoiceSelected에서 '각 Edition에 대해 설명해줘' 선택(I01_installer_server_type_check_lite index==2 / _full index==1, :141/:159). · ScenarioInstallerManager.Scenario_I01_1_InstallServer()에서 InstallerManager.Instance.RunInstaller() 호출 지점(:389) 실행 시 ReportFlag. (Lite 경로 RunInstallerLite :375는 현재 C99_NotReady로 우회 중이므로 Full 설치 RunInstaller가 유효 이벤트) · ScenarioInstallerManager.Scenario_I02_InstallComplete() (:433) 진입 시 ReportFlag. 트리거: CheckInstallStatus()가 상태를 lite/full로 감지(:58-63) → 코루틴 시작. · JarvisServerManager.RunJarvisServer() (JarvisServerManager.cs:97) 최초 실행 시 ReportFlag. RunJarvisServerWithCheck()(:45) 및 InstallStatusManager(:375)/설치완료 코루틴(:448) 경유. 신규 영구 플래그로 1회 판별._

### CV — 대화 · 신규 34개 (기존 9개에 이어 CV0010부터)

| id | name | type | 목표 | 보상 | 한국어 | English | 日本語 | 연동 | 검증 | 원천 |
|----|------|------|------|------|--------|---------|--------|------|------|------|
| `CV0010` | emotion_diversity | Tiered | 3 / 5 / 6 | g50 / g100 / i1×1 | 감정의 스펙트럼 | Spectrum of Emotions | 感情のスペクトル | 🟡 | ✓ | CV |
| `CV0011` | thinking_response_count | Increment | 10N | g20 | 깊은 생각 | Deep in Thought | 考え中のアイコ | 🟢 | ✓ | CV |
| `CV0012` | web_search_use_count | Increment | 10N | g20 | 궁금한 건 찾아봐요 | Web Explorer | 気になることは検索 | 🟢 | ✓ | CV |
| `CV0013` | web_search_forced_use | Increment | 5N | g30 | 직접 검색 요청 | Search on Demand | 自分で検索リクエスト | 🟢 | ✓ | CV |
| `CV0014` | image_attach_use | Increment | 15N | g25 | 사진으로 대화하기 | Picture Talk | 写真でおしゃべり | 🟢 | ✓ | CV |
| `CV0015` | image_source_diversity | OneTime | 1 | g50 | 클립보드와 스크린샷, 둘 다 | Both Ways to Share | クリップボードもスクショも | 🟢 | ✓ | CV |
| `CV0016` | chat_regenerate_use | Increment | 8N | g20 | 다시 한번 들어볼래 | Ask Again | もう一度聞いてみる | 🟡 | ✓ | CV |
| `CV0017` | delete_recent_dialogue | OneTime | 1 | g30 | 방금 대화 되돌리기 | Second Thoughts | さっきの会話を巻き戻し | 🟢 | ✓ | CV |
| `CV0018` | language_switch_all | OneTime | 3 | g40 | 세 가지 언어로 | Trilingual Chat | 三か国語で会話 | 🟢 | ✓ | CV |
| `CV0019` | ai_choice_click | Increment | 10N | g20 | AI의 추천을 따라서 | Following AI's Lead | AIのおすすめに従って | 🟢 | ✓ | CV |
| `CV0020` | character_diversity | Tiered | 2 / 3 / 4 | g50 / g150 / g300 | 다양한 얼굴들과의 대화 | Many Faces, Many Talks | いろんな子とおしゃべり | 🟡 | ✓ | CV |
| `CV0021` | furigana_use | OneTime | 1 | g30 | 후리가나의 비밀 | Furigana Secret | ふりがなのひみつ | 🟡 | ✓ | CV |
| `CV0022` | first_choice_click | OneTime | 1 | g50 | 첫 선택의 순간 | First Choice | はじめての選択 | 🟡 | ✓ | CV |
| `CV0023` | ai_choice_recall | OneTime | 1 | g30 | 다시 볼래요 | One More Look | もう一度見せて | 🟡 | ✓ | CV |
| `CV0024` | st_first_smalltalk | OneTime | 1 | g50 | 아이코의 첫 선톡 | AICO's First Hello | アイコからの初トーク | 🟡 | ✓ | ST |
| `CV0025` | st_receive_many | Increment | 10N | g30 | 먼저 말 걸어주는 아이코 | Chatty Companion | 話しかけてくれるアイコ | 🟡 | ✓ | ST |
| `CV0026` | st_enable_auto | OneTime | 1 | g30 | 자동 선톡 켜기 | Turn On Auto Chat | 自動トークをオン | 🟡 | ✓ | ST |
| `CV0027` | first_web_search | OneTime | 1 | g50 | 첫 실시간 검색 | First Web Search | はじめてのウェブ検索 | 🟡 | ✓ | WB |
| `CV0028` | md_aropla_first | OneTime | 1 | g80 | 아로플라 첫 대화 | First Aropla Chat | はじめてのアロプラ会話 | 🟡 | ✓ | MD |
| `CV0029` | md_operator_first | OneTime | 1 | g80 | 오퍼레이터 모드 첫 진입 | Enter Operator Mode | オペレーターモード初体験 | 🟡 | ✓ | MD |
| `CV0030` | md_switch | Increment | 5N | g40 | 모드 자주 바꾸기 | Mode Switcher | モードを切り替える | 🟡 | ✓ | MD |
| `CV0031` | md_plana_first_reply | OneTime | 1 | g100, i1×1 | 프라나의 첫 대답 | Plana's First Reply | プラナのはじめての返事 | 🟡 | ✓ | MD |
| `CV0032` | voice_conversation_milestone | Tiered | 10 / 50 / 150 | g100 / g250 / g500 | 목소리로 대화하기 | Talk It Out Loud | 声で話してみよう | 🟡 | ✓ | VC |
| `CV0033` | vad_first_use | OneTime | 1 | g40 | 자동 음성감지 첫 시도 | Hands-Free Debut | 自動音声検知デビュー | 🟡 | ✓ | VC |
| `CV0034` | max_recording_marathon | OneTime | 1 | g60 | 끝까지 말하기 | Talk Till the Timer | 最後まで話しきる | 🟡 | ✓ | VC |
| `CV0035` | play_first_round | OneTime | 1 | g30 | 게임을 시작하다 | Game On | ゲームスタート | 🟢 | ✓ | GM |
| `CV0036` | ask_questions | Increment | 10N | g20 | 질문 탐정 | Question Detective | 質問の探偵 | 🟢 | ✓ | GM |
| `CV0037` | win_secret_answer | OneTime | 1 | g100 | 첫 승리의 기쁨 | First Victory | はじめての勝利 | 🟢 | ✓ | GM |
| `CV0038` | discover_new_secrets | Tiered | 5 / 15 / 30 | g50 / g150 / g300 | 새로운 정답들 | New Discoveries | 新しい答えたち | 🟢 | ✓ | GM |
| `CV0039` | chat_history_open | Tiered | 1 / 5 / 20 | g30 / g80 / g150 | 지난 대화 들춰보기 | Look Back at Old Chats | 昔の会話を見返す | 🟡 | ✓ | ME |
| `CV0040` | reset_memory | OneTime | 1 | g50 | 기억을 새로 시작하기 | Start Fresh Memories | 記憶をリセット | 🟡 | ✓ | ME |
| `CV0041` | edit_persona_card | OneTime | 1 | g40 | 유저카드 다시 써보기 | Rewrite a Persona Card | ユーザーカードを書き直す | 🟡 | ✓ | ME |
| `CV0042` | menu_open_master | Tiered | 1 / 20 / 100 | g20 / g100 / g300 | 메뉴의 달인 | Menu Master | メニューの達人 | 🟢 | ✓ | NT |
| `CV0043` | radial_menu_debut | Tiered | 1 / 10 / 30 | g20 / g80 / g200 | 라디얼 메뉴 첫걸음 | Radial Menu Debut | ラジアルメニュー初挑戦 | 🟡 | ✓ | NT |

_주요 훅: EmotionManager.ShowEmotionFromEmotion(string) (EmotionManager.cs:67) 호출 지점(APIManager.FetchStreamingData/PrepareConversationReplyUi/CallSmallTalkStream 경유)에서 수신 emotion 문자열을 distinct Set에 누적, ReportBest(distinctCount) · APIManager replyType=="thinking" 분기(APIManager.cs:253, 578, 1037)→NoticeManager.Notice("thinking") 직후 Report · APIManager replyType=="webSearch" 분기(APIManager.cs:257, 1041)→NoticeManager.Notice("webSearch") 직후 Report · ChatHandler.HandleInputWebSubmitButton()에서 GameManager.Instance.isWebSearchForced=true 설정 직후(ChatHandler.cs:137) Report · APIManager.AttachImageToRequest(...) (APIManager.cs:711) 실제 호출 시 Report · ChatBalloonManager.GetImageSource() (ChatBalloonManager.cs:500)가 반환하는 "clipboard"/"screenshot"를 Set으로 추적, 둘 다 등장 시 ReportFlag · AnswerBalloonManager.ChatRegenerate() (AnswerBalloonManager.cs:195) 호출 시 Report · AnswerBalloonManager.DeleteRecentDialogue() (AnswerBalloonManager.cs:186)→MemoryManager.DeleteRecentDialogue() (MemoryManager.cs:262) 진입 시 ReportFlag · SettingManager.SetUiLanguage() (SettingManager.cs:263)에서 선택한 ui_language_idx를 distinct Set에 누적, size==3 도달 시 ReportFlag · ChoiceManager.OnClickChoice(int) (ChoiceManager.cs:176)에서 curChoiceScenario=="AI_CHOICE" 분기(cs:200) 진입 시 Report · APIManager.OnFinalResponseReceived (APIManager.cs:1505)에서 CharManager.Instance.GetNickname(GetCurrentCharacter()) (CharManager.cs:906/332)를 distinct Set에 누적, ReportBest · APIManager.CallFuriganaAPIAsync(text) (APIManager.cs:1976) 변환 성공(JP) 최초 시 ReportFlag_

### AF — 교감 · 신규 22개 (기존 5개에 이어 AF0006부터)

| id | name | type | 목표 | 보상 | 한국어 | English | 日本語 | 연동 | 검증 | 원천 |
|----|------|------|------|------|--------|---------|--------|------|------|------|
| `AF0006` | pat_head_first | OneTime | 1 | g50 | 첫 쓰다듬 | First Pat | はじめてのなでなで | 🟡 | ✓ | AF |
| `AF0007` | pat_head_master | Tiered | 50 / 200 / 500 | g100 / g250 / g500 | 쓰다듬기 달인 | Petting Master | なでなでマスター | 🟡 | ✓ | AF |
| `AF0008` | arona_heart_eyes | OneTime | 1 | g80, i1×1 | 아로나의 하트눈 | Arona's Heart Eyes | アロナのハート目 | 🟡 | ✓ | AF |
| `AF0009` | emotion_first_reaction | OneTime | 1 | g50 | 첫 감정의 순간 | First Spark of Emotion | はじめての感情 | 🟡 | ✓ | AF |
| `AF0010` | emotion_reactions_cumulative | Increment | 10N | g20 | 다채로운 표정 보기 | So Many Reactions | いろんな表情を見る | 🟡 | ✓ | AF |
| `AF0011` | listen_reaction_first | OneTime | 1 | g50 | 귀 기울이는 아이코 | Aiko Lends an Ear | 耳を傾けるアイコ | 🟡 | ✓ | AF |
| `AF0012` | listen_together | Increment | 10N | g20 | 언제나 들어주는 아이코 | Always Listening | いつも聞いてくれるアイコ | 🟡 | ✓ | AF |
| `AF0013` | voice_attention_first | OneTime | 1 | g50 | 목소리에 귀 기울이기 | Listening to Your Voice | 声に耳を澄ます | 🟡 | ✓ | AF |
| `AF0014` | favorite_first | OneTime | 1 | g50 | 첫 즐겨찾기 | First Favorite | はじめてのお気に入り | 🟡 | ✓ | AF |
| `AF0015` | favorite_collector | Tiered | 3 / 10 / 20 | g100 / g280, i1×1 / g550, i2×1 | 마음에 든 아이들 | My Favorites | お気に入りの子たち | 🟡 | ✓ | AF |
| `AF0016` | favorite_filter_first | OneTime | 1 | g40 | 즐겨찾기만 보기 | Favorites Only | お気に入りだけ表示 | 🟡 | ✓ | AF |
| `AF0017` | outfit_swap_first | OneTime | 1 | g80 | 처음 갈아입히기 | First Outfit Change | はじめての着せ替え | 🟡 | ✓ | AF |
| `AF0018` | wardrobe_collector | Tiered | 5 / 15 / 30 | g100 / g250 / g450, i1×1 | 옷장 정복기 | Wardrobe Climber | ワードローブ制覇 | 🟡 | ✓ | AF |
| `AF0019` | char_resize_first | OneTime | 1 | g50 | 딱 맞는 크기로 | Just the Right Size | ぴったりサイズに | 🟡 | ✓ | AF |
| `AF0020` | showcase_snapshot_first | OneTime | 1 | g50 | 꾸민 모습 찰칵 | Snapshot Your AICO | 着せ替え姿をパチリ | 🟡 | ✓ | AF |
| `AF0021` | gravity_awakening | OneTime | 1 | g30 | 살짝 놓아주기 | Let Go Gently | そっと手放す | 🟡 | ✓ | AF |
| `AF0022` | dance_first | OneTime | 1 | g50 | 첫 댄스 요청 | First Dance | はじめてのダンス | 🟡 | ✓ | AC |
| `AF0023` | dance_lover | Increment | 5N | g30 | 댄스 마니아 | Dance Fanatic | ダンス好き | 🟡 | ✓ | AC |
| `AF0024` | dance_by_shortcut | OneTime | 1 | g50 | 단축키로 춤추기 | Dance by Shortcut | ショートカットでダンス | 🟡 | ✓ | AC |
| `AF0025` | walk_first | OneTime | 1 | g50 | 첫 걸음마 | First Steps | はじめてのお散歩 | 🟡 | ✓ | AC |
| `AF0026` | daily_walk | Tiered | 3 / 10 / 25 | g100 / g250 / g500 | 산책 애호가 | Daily Stroller | お散歩好き | 🟡 | ✓ | AC |
| `AF0027` | stop_on_command | OneTime | 1 | g50 | 멈춰! | Freeze! | ストップ！ | 🟡 | ✓ | AC |

_주요 훅: DragHandler.PatHead() (DragHandler.cs L183) → EmotionBalloonManager.Instance.ShowEmotionBalloon(this.gameObject, "Love") (L196) 최초 진입 시 MissionList.ReportFlag(id) · DragHandler.PatHead() (DragHandler.cs L183) 진입 시 MissionList.Report(id,1) 누적 · DragHandler.PatHead() 내 nickname=="arona" 분기 → EmotionManager.Instance.ShowEmotion("><") (DragHandler.cs L223-225) 최초 실행 시 MissionList.ReportFlag(id) · EmotionManager.ShowEmotionFromEmotion(ai_info_emotion) 최초 호출 지점(APIManager.cs L335 / L1185 / L1782) 중 한 곳에 MissionList.ReportFlag(id) · EmotionManager.ShowEmotionFromEmotion(...) 호출부(APIManager.cs L335/L1185/L1782)에서 MissionList.Report(id,1) 누적 · ChatBalloonManager.cs L290/294 → AnimationManager.Instance.Listen() → EmotionManager.ShowEmotionFromAction("listen") (AnimationManager.cs L109) 최초 실행 시 MissionList.ReportFlag(id) · ChatBalloonManager.cs L290/294의 AnimationManager.Instance.Listen() 호출 지점에서 MissionList.Report(id,1) 누적 · MicrophoneManager.cs L105 NoticeManager.Instance.ShowNoticeEmotionBalloon("Listen", maxRecordingDuration) 최초 호출 시 MissionList.ReportFlag(id) · ChangeCharCardController.cs L129 (charData.isFavorite = !charData.isFavorite) 및 ChangeCharListSlotController.cs L155 — false→true 전환 시 MissionList.ReportFlag(id) · ChangeCharManager의 favoriteCharacterNames 집합(ChangeCharManager.cs L487/L507) 크기를 MissionList.ReportBest(id, count)로 전달 · ChangeCharManager.ToggleFavoriteFilter() (ChangeCharManager.cs L369) 최초 호출 시 MissionList.ReportFlag(id) · CharManager.ChangeCharacter2DGeneral(clothesInfo) (CharManager.cs L744, 호출: ChangeCharCardController.cs L281 / ChangeCharListSlotController.cs L235) 최초 호출 시 MissionList.ReportFlag(id)_

### PR — 생활 · 신규 35개 (기존 8개에 이어 PR0009부터)

| id | name | type | 목표 | 보상 | 한국어 | English | 日本語 | 연동 | 검증 | 원천 |
|----|------|------|------|------|--------|---------|--------|------|------|------|
| `PR0009` | set_alarm | Tiered | 1 / 5 / 10 | g50 / g150 / g300, i1×1 | 알람 맞추기 | Set an Alarm | アラームをセット | 🟡 | ✓ | PR |
| `PR0010` | alarm_ring | Increment | 3N | g15 / level | 알람이 울렸어요 | Alarm Goes Off | アラームが鳴った | 🟢 | ✓ | PR |
| `PR0011` | name_alarm | OneTime | 1 | g50 | 알람에 이름 붙이기 | Name Your Alarm | アラームに名前をつける | 🟡 | ✓ | PR |
| `PR0012` | create_timer | OneTime | 1 | g60 | 첫 타이머 만들기 | Create Your First Timer | はじめてのタイマー作成 | 🟡 | ✓ | PR |
| `PR0013` | run_timer | Increment | 10N | g15 / level | 타이머 돌리기 | Run the Timer | タイマーを走らせる | 🟡 | ✓ | PR |
| `PR0014` | pomodoro_debut | OneTime | 1 | g50 | 포모도로 시작하기 | Start a Pomodoro | ポモドーロを始める | 🟡 | ✓ | PR |
| `PR0015` | pomodoro_complete | Tiered | 1 / 5 / 20 | g80 / g200, i1×1 / g500 | 포모도로 완주하기 | Complete Pomodoro Sessions | ポモドーロをやり切る | 🟡 | ✓ | PR |
| `PR0016` | open_todo_list | OneTime | 1 | g40 | 할 일 목록 열기 | Open Your To-Do List | ToDoリストを開く | 🟡 | ✓ | PR |
| `PR0017` | add_todo | Tiered | 10 / 30 / 60 | g100 / g250 / g450 | 할 일 등록하기 | Add To-Dos | ToDoを追加する | 🟡 | ✓ | PR |
| `PR0018` | complete_todo | Increment | 10N | g30 / level | 할 일 체크하기 | Check Off To-Dos | ToDoをチェックする | 🟡 | ✓ | PR |
| `PR0019` | reorder_todo | OneTime | 1 | g50 | 할 일 순서 바꾸기 | Reorder Your To-Dos | ToDoを並び替える | 🟡 | ✓ | PR |
| `PR0020` | pick_calendar_date | OneTime | 1 | g30 | 캘린더에서 날짜 고르기 | Pick a Date on the Calendar | カレンダーで日付を選ぶ | 🟢 | ✓ | PR |
| `PR0021` | calendar_date_variety | Tiered | 5 / 15 / 30 | g50 / g120 / g250 | 여러 날짜 둘러보기 | Browse Many Dates | いろんな日付をめぐる | 🟡 | ✓ | PR |
| `PR0022` | flip_calendar_month | Increment | 8N | g20 / level | 달력 넘겨보기 | Flip Through the Months | カレンダーをめくる | 🟡 | ✓ | PR |
| `PR0023` | jukebox_first_play | OneTime | 1 | g50 | 첫 곡 재생하기 | Play Your First Track | はじめての一曲を再生 | 🟡 | ✓ | PR |
| `PR0024` | jukebox_track_select | Increment | 10N | g15 / level | 원하는 곡 골라 듣기 | Pick Tracks to Play | 好きな曲を選んで再生 | 🟡 | ✓ | PR |
| `PR0025` | jukebox_mode_switch | OneTime | 1 | g40 | 재생 모드 바꾸기 | Switch the Playback Mode | 再生モードを切り替える | 🟡 | ✓ | PR |
| `PR0026` | jukebox_open_ambience | OneTime | 1 | g30 | 환경음 패널 열기 | Open the Ambience Panel | 環境音パネルを開く | 🟡 | ✓ | PR |
| `PR0027` | agent_first_command | OneTime | 1 | g100, i1×1 | AI에게 화면 조작 맡기기 | Let AI Take the Controls | AIに画面操作をまかせる | 🟡 | ✓ | AG |
| `PR0028` | agent_success_count | Increment | 5N | g40 / level | AI 대행 성공 쌓기 | Rack Up AI Successes | AI代行の成功を重ねる | 🟡 | ✓ | AG |
| `PR0029` | agent_type_text | Tiered | 1 / 50 / 200 | g50 / g180 / g450 | AI가 대신 타이핑하기 | Let AI Type for You | AIに代わりにタイピングさせる | 🟡 | ✓ | AG |
| `PR0030` | save_first_skill | OneTime | 1 | g80, i1×1 | 나만의 첫 스킬 저장 | Save Your First Skill | はじめてのスキルを保存 | 🟡 | ✓ | SK |
| `PR0031` | browse_skills | Increment | 10N | g10 / level | 스킬 카탈로그 둘러보기 | Browse the Skill Catalog | スキルカタログをめぐる | 🟢 | ✓ | SK |
| `PR0032` | use_first_skill | OneTime | 1 | g80, i1×1 | 첫 스킬 실행하기 | Run Your First Skill | はじめてのスキルを実行 | 🟡 | ✓ | SK |
| `PR0033` | vision_select_area | OneTime | 1 | g50 | 화면 영역 지정하기 | Select a Screen Area | 画面の範囲を指定する | 🟡 | ✓ | VS |
| `PR0034` | vision_run_ocr | Increment | 10N | g25 / level | 화면 텍스트 읽어내기 | Read Text from the Screen | 画面のテキストを読み取る | 🟡 | ✓ | VS |
| `PR0035` | vision_share_clipboard | Tiered | 1 / 10 / 30 | g30 / g100 / g250 | 클립보드 이미지로 대화하기 | Chat with a Clipboard Image | クリップボード画像で話す | 🟡 | ✓ | VS |
| `PR0036` | hide_to_tray | OneTime | 1 | g50 | 트레이로 숨기기 | Hide to the Tray | トレイに隠れる | 🟡 | ✓ | WM |
| `PR0037` | toggle_gravity | OneTime | 1 | g30 | 중력 켜기 | Turn On Gravity | 重力をオンにする | 🟡 | ✓ | WM |
| `PR0038` | click_sparkles | Increment | 20N | g10 / level | 클릭으로 반짝이 터뜨리기 | Pop Sparkles with Clicks | クリックでキラキラを弾けさせる | 🟡 | ✓ | WM |
| `PR0039` | hotkey_dance | Tiered | 1 / 10 / 50 | g50 / g150 / g400 | 단축키로 춤추게 하기 | Trigger a Dance with a Hotkey | ショートカットで踊らせる | 🟡 | ✓ | HK |
| `PR0040` | custom_hotkey_binding | OneTime | 1 | g50 | 나만의 단축키 설정하기 | Set a Custom Hotkey | 自分だけのショートカットを設定 | 🟡 | ✓ | HK |
| `PR0041` | open_settings_panel | OneTime | 1 | g30 | 설정 화면 열기 | Open the Settings Panel | 設定画面を開く | 🟡 | ✓ | SE |
| `PR0042` | explore_settings_tabs | Tiered | 3 / 5 / 8 | g30 / g60 / g100 | 설정 탭 둘러보기 | Explore the Settings Tabs | 設定タブをめぐる | 🟡 | ✓ | SE |
| `PR0043` | switch_ui_language | OneTime | 1 | g20 | 화면 언어 바꾸기 | Switch the UI Language | 表示言語を切り替える | 🟡 | ✓ | SE |

_주요 훅: AlarmManager.AddDailyAlarm(string title,int hour,int minute,int second,string audioClipId) 성공 시 Report(id,1). 생성 지점에서 누적(삭제로 리스트가 줄어도 값 유지). · AlarmManager.AlarmRang 이벤트(event Action<AlarmItem>, L12)에 리스너 구독해 발행마다 Report(id,1). AlarmUI가 이미 구독 중이라 배선 최소. · AlarmManager.UpdateAlarmTitle(string id,string title) 내부, 빈 제목→비어있지 않은 값으로 바뀌는 분기에서 ReportFlag(id). · AlarmManager.AddRelativeTimer(string title,int durationSeconds,string audioClipId) 최초 성공 호출 시 ReportFlag(id). UI 진입점 2곳이 모두 이 메서드로 수렴. · AlarmManager.StartRelativeTimer(string id) 호출마다 Report(id,1). 상세패널/미니위젯/리스트토글 4개 UI 경로가 이 단일 허브로 수렴. · PomodoroTimer.OnStart() (L153) 호출 시 ReportFlag(id). · PomodoroTimer.OnPhaseComplete()(private IEnumerator, L291) 내 phase==Phase.Work 완료 분기 currentCycle++ 지점(L303)에서 Report(id,1). · JarvisTodoListUI.Show(DateTime date) (L63) 호출 시 ReportFlag(id). · JarvisTodoStore.AddItem(DateTime date,[string time,]string content) (L96/L101) 성공 시 Report(id,1). · JarvisTodoStore.SetCompleted(string id,bool isCompleted) (L122)의 wasCompleted==false→true 분기(L136)에서 Report(id,1). 이미 변경 가드가 있어 중복 방지. · JarvisTodoStore.Reorder(DateTime date,List<string> orderedIds) (L177) 호출 시 ReportFlag(id). · JarvisCalendarUI.DateSelected 이벤트(event Action<DateTime>, L9) 구독 후 최초 발생 시 ReportFlag(id). OnDayClicked(L217)에서 발행._

### CH — 도전 · 신규 15개 (기존 8개에 이어 CH0009부터)

| id | name | type | 목표 | 보상 | 한국어 | English | 日本語 | 연동 | 검증 | 원천 |
|----|------|------|------|------|--------|---------|--------|------|------|------|
| `CH0009` | item_one_collector | Tiered | 5 / 20 / 50 | g80 / g250 / g600 | 수집가의 시작 | Collector's Start | コレクター、はじめの一歩 | 🟢 | ✓ | CH |
| `CH0010` | full_set_collection | OneTime | 1 | g120, i1×1 | 삼색 컬렉션 | One of Each | 三色そろえて | 🟢 | ✓ | CH |
| `CH0011` | time_with_aico | Tiered | 60 / 600 / 3000 | g100 / g400 / g1000 | 함께한 시간 | Time Together | 一緒に過ごした時間 | 🟡 | ✓ | CH |
| `CH0012` | server_first_connect | OneTime | 1 | g100 | AI와 첫 연결 | First Connection | AIとの初めての接続 | 🟢 | ✓ | XX |
| `CH0013` | setup_tutorial_complete | OneTime | 1 | g150 | 설정 마법사 완주 | Setup Complete | セットアップ完了 | 🟡 | ✓ | XX |
| `CH0014` | screenshot_capture | Tiered | 1 / 10 / 30 | g50 / g150 / g300 | 화면 캡처하기 | Screen Capture | 画面をキャプチャ | 🟡 | ✓ | XX |
| `CH0015` | edition_upgrade | Tiered | 1 / 2 / 3 | g100 / g250 / g400, i2×1 | 에디션 업그레이드 | Edition Upgrade | エディションアップグレード | 🟡 | ✓ | XX |
| `CH0016` | dlc_download_complete | Tiered | 1 / 3 / 6 | g100 / g250 / g400, i1×1 | 새로운 모습 받기 | New Look Unlocked | 新しい姿を手に入れる | 🟡 | ✓ | XX |
| `CH0017` | clipboard_ai_share | OneTime | 1 | g80 | 클립보드로 대화하기 | Chat via Clipboard | クリップボードで会話 | 🟡 | ✓ | XX |
| `CH0018` | mr_character_menu_open | OneTime | 1 | g50 | 커스터마이징 메뉴 열기 | Open the Customization Menu | 着せ替えメニューを開く | 🟡 | ✓ | MR |
| `CH0019` | mr_gentle_pat | Increment | 10N | g20 | 다정한 손길 | Gentle Hands | やさしくなでる | 🟡 | ✓ | MR |
| `CH0020` | mr_smash_combo | Tiered | 10 / 30 / 50 | g100 / g300 / g500 | 꿀밤 콤보왕 | Smash Combo King | コツンコンボ王 | 🟡 | ✓ | MR |
| `CH0021` | mr_triple_touch | OneTime | 1 | g120 | 세 가지 손길 마스터 | Triple Touch Master | 三種の触れ合いマスター | 🟡 | ✓ | MR |
| `CH0022` | mr_first_beat | OneTime | 1 | g50 | 첫 곡 재생 | First Beat | はじめての一曲 | 🟡 | ✓ | MR |
| `CH0023` | mr_keyboard_typist | OneTime | 1 | g40 | 가상 키보드로 첫 입력 | First Words Typed | 仮想キーボードで初入力 | 🟢 | ✓ | MR |

_주요 훅: MissionList.UpdateDerived()에 SetCurrent(id, InventoryManager.Instance.GetItem(1)) 한 줄 추가 — InventoryManager.GetItem(int slot)(InventoryManager.cs:104) 이미 존재, CH0008(ItemTotal) 배선과 동일 패턴(MissionList.cs:336). · MissionList.UpdateDerived()에 SetCurrent(id, (inv.GetItem(1)>=1 && inv.GetItem(2)>=1 && inv.GetItem(3)>=1)?1:0) 추가 — 기존 getter 3개 조합, 신규 저장필드 불필요(InventoryManager.cs:104). · MissionList.UpdateDerived()에 SetCurrent(id, (int)(GlobalTimeVariableManager.Instance.totalPlaySeconds/60f)) 추가 — totalPlaySeconds 필드 존재(GlobalTimeVariableManager.cs:22). · JarvisServerManager.CheckHealthAndNotify() 성공 분기에서 ScenarioCommonManager.Instance.Run_C01_ServerStarted() 호출 지점(JarvisServerManager.cs:79)에 ReportFlag 추가. · ScenarioTutorialManager.Scenario_A99_ConfigEnd()에서 SettingManager.Instance.settings.isTutorialCompleted=true 설정 직후(ScenarioTutorialManager.cs:843)에 Report 추가. · ScreenshotManager.SaveAndShowScreenshot()(ScreenshotManager.cs:621) 완료 지점에 Report(id, 1) 추가. · InstallStatusManager.SetToLite()/SetToFull()(:168/:187) 후 GetInstallStatusIndex()(:125) 반환값을 ReportBest(id, index). · DownloadManager.RequestAddressableDownload(address, expectedSize, onComplete)(DownloadManager.cs:661)의 onComplete(true) 콜백에서 Report(id, 1). · ClipboardManager.OnClipboardChanged() → ChatBalloonManager.Instance.SetLastImageSource("clipboard")(ClipboardManager.cs:144) 이후 실제 채팅 전송이 확정되는 지점에 ReportFlag. (SetLastImageSource: ChatBalloonManager.cs:484) · MRCharacterMenu.OpenMenu(MRSpineCharacterController, Vector3?)(MRCharacterMenu.cs:162) 최초 호출 시 ReportFlag. · MRSpineCharacterController.EnterState(State.Pat) 진입 지점(MRSpineCharacterController.cs:350)에서 Report(id, 1). · MRSpineCharacterController의 _smashHitCount++ 지점(MRSpineCharacterController.cs:659)에서 ReportBest(id, _smashHitCount)._

---

## 배선 가이드 / 다음 액션

### 즉시 배선 (🟢 = 이벤트 존재)
구독만 하면 되는 것들: 알람(`AlarmManager.AlarmsChanged/AlarmRang`), 할일(`JarvisTodoStore.Changed`), 인벤토리(`InventoryManager.InventoryChanged`), 메타/카테고리 완료(`MissionList.UpdateDerived` 자동).

### Report 한 줄 (🟡)
실행 지점에 `MissionList.Instance.Report("ID")` / `ReportFlag` / `ReportBest` 추가. 표의 '주요 훅' 참고.
- 대화/감정/웹검색/이미지: `APIManager` 응답 파싱부(ai_info.emotion, intent_info.is_intent_web, type)
- 선톡: `GlobalTimeVariableManager.TryTriggerSmallTalk` / 설정 `isCharAutoSmallTalk`
- 에이전트/스킬/캐릭터액션: `ApiAgentFunctionManager.ExecuteAction` (functionName 분기)
- 챗모드: `ChatModeManager.SetMode`
- 캐릭터/의상/즐겨찾기: `CharManager`/`ChangeCharManager`
- 쓰다듬기/찌르기: `DragHandler`/`ClickHandler`
- 주크박스: `JukeboxView.PlayTrack/Show`

### 검증 ? 항목
표의 '검증'이 ?인 미션은 훅 시그니처가 미확정이니 **배선 전에 해당 매니저를 열어 메서드명/이벤트를 최종 확인**하고 표를 갱신할 것.

### 밸런싱 미결
- 감정/키워드 카운트는 AI 응답 신뢰도 의존 → 흔들리면 목표치 완화.
- 시간대/요일 미션은 로컬시각 판정 규칙 확정 필요.
- 접속/플레이타임은 과거 삭제 이력 → 재도입 여부 재확인.
- item1~3 정체 미확정 → 수집 문구는 확정 후 다듬기.
- 보상 수치는 초안(경제 모델 없음, 플레이스홀더).

### 더 필요하면
🟠(보강필요)·🔴(신규시스템) 후보 215개는 [MISSION_Pool_v3.md](MISSION_Pool_v3.md)에 그대로 있음. 포모도로 완료 이벤트·친밀도 영속화·상점 등을 만들 때 함께 채택.