# Mission 카탈로그 2 — 확장 브레인스토밍 (v3, 워크플로 전면 조사)

> **목적**: 코드베이스를 시스템별로 훑어(멀티에이전트 워크플로) AICO가 실제로 가진 기능에 근거한 **미션 대량 후보**를 도출.
> 여기서 고르고 다듬어 `MissionDatabase.Build()`(코드)에 1줄씩 반영한다.
> 작성: 2026-07-03 · 상태: **브레인스토밍 초안(대량)** · 생성: 31개 도메인 조사 + 6개 렌즈 갭필

## 스키마 / 범례

- **id**: 2영문(카테고리)+4숫자. 확정 후 변경 금지. (이 문서의 번호는 자동 부여 — 채택 시 재정렬 가능)
- **type**: OneTime / Increment(aN+b) / Tiered(a/b/c). 목표는 전부 int.
- **보상**: g=gold, i1~i3=item1~3.
- **연동 난이도**: 🟢 바로(이벤트/카운터 존재) · 🟡 훅필요(Report 한 줄) · 🟠 보강필요(카운팅 지점 신설) · 🔴 신규시스템(개념 자체 없음).

> ⚠️ 이 문서는 **아이디어 풀**이다. 중복/약한 항목이 섞여 있을 수 있으니 채택 단계에서 추린다.
> 기존 `MISSION_Catalog.md`(38개)는 건드리지 않으며, 신규 id는 각 카테고리 기존 최대번호 다음부터 이어 붙인다.

## 카테고리 지도

- **기존 5탭**: OB 첫걸음 · CV 대화 · AF 교감 · PR 생활 · CH 도전
- **AI 비서 코어(신규)**: ST 선톡 · WB 웹검색 · AG 에이전트조작 · AC 캐릭터액션 · SK AI스킬 · MD 대화모드
- **확장(신규)**: VC 음성 · GM 미니게임 · VS 비전 · WM 창모드 · HK 핫키 · NT 알림/메뉴 · SE 설정 · ME 기억 · MR MR/XR · XX 기타

---

## 합계 (신규)

| 코드 | 카테고리 | 기존 | 신규 |
|------|----------|-----:|-----:|
| OB | 첫걸음 | 8 | 18 |
| CV | 대화 | 9 | 58 |
| AF | 교감 | 5 | 87 |
| PR | 생활 | 8 | 88 |
| CH | 도전 | 8 | 19 |
| ST | 선톡(SmallTalk) | 0 | 20 |
| WB | 웹검색·지식 | 0 | 16 |
| AG | AI 에이전트 조작 | 0 | 18 |
| AC | 캐릭터 액션 | 0 | 15 |
| SK | AI 스킬 | 0 | 21 |
| MD | 대화 모드 | 0 | 19 |
| VC | 음성 | 0 | 20 |
| GM | 미니게임 | 0 | 17 |
| VS | 비전/화면 | 0 | 21 |
| WM | 창·표시 | 0 | 13 |
| HK | 핫키/입력 | 0 | 19 |
| NT | 알림/메뉴 | 0 | 20 |
| SE | 설정·언어·모델 | 0 | 21 |
| ME | 기억 | 0 | 25 |
| MR | MR/XR | 0 | 20 |
| XX | 기타(미분류) | 0 | 17 |

**신규 미션 합계: 572개** (기존 38개 별도).

---

## 미션 후보 (카테고리별)

### OB — 첫걸음 · 신규 18개 (기존 8개에 이어 OB0009부터)

| id | name | type | 목표 | 보상 | 한국어 | English | 日本語 | 연동 |
|----|------|------|------|------|--------|---------|--------|------|
| `OB0009` | meet_aico | OneTime | 1 | g20 | 아이코와 첫 만남 | Meeting AICO for the First Time | アイコとの最初の出会い | 🟠 |
| `OB0010` | first_chat | OneTime | 1 | g30 | 아이코와 첫 대화 나누기 | A First Chat with AICO | アイコと初めて話してみる | 🟡 |
| `OB0011` | open_chat_balloon | OneTime | 1 | g20 | 대화창 처음 열어보기 | Opening the Chat Bubble | 会話バブルを開いてみる | 🟡 |
| `OB0012` | change_character | OneTime | 1 | g30 | 캐릭터 바꿔보기 | Trying a New Character | キャラクターを変えてみる | 🟡 |
| `OB0013` | change_language | OneTime | 1 | g20 | 언어 설정 바꿔보기 | Switching the Language | 言語設定を変えてみる | 🟡 |
| `OB0014` | pat_head_first | OneTime | 1 | g20 | 머리 쓰다듬어 주기 | Petting AICO's Head | 頭をなでてあげる | 🟡 |
| `OB0015` | open_settings | OneTime | 1 | g20 | 설정 화면 열어보기 | Opening Settings | 設定画面を開いてみる | 🟡 |
| `OB0016` | wear_accessory | OneTime | 1 | g40, i1×1 | 액세서리 처음 착용하기 | Wearing an Accessory | アクセサリーを初めて着けてみる | 🟡 |
| `OB0017` | open_jukebox | OneTime | 1 | g30 | 주크박스 열어보기 | Opening the Jukebox | ジュークボックスを開いてみる | 🟠 |
| `OB0018` | start_installer | OneTime | 1 | g30 | 설치 안내 시작하기 | Starting the Setup Guide | インストール案内を始める | 🟡 |
| `OB0019` | explore_editions | Tiered | 1 / 5 / 15 | g30 / g100 / g200 | 에디션 둘러보기 | Exploring the Editions | エディションを見て回る | 🟡 |
| `OB0020` | install_server | OneTime | 1 | g60, i1×1 | 서버 설치 실행하기 | Running the Server Installer | サーバーのインストールを実行する | 🟢 |
| `OB0021` | installation_complete | OneTime | 1 | g80, i2×1 | 설치 완료의 순간 | The Moment Installation Completes | インストール完了の瞬間 | 🟢 |
| `OB0022` | start_server_first | OneTime | 1 | g40 | 서버 처음 켜보기 | Starting the Server for the First Time | サーバーを初めて起動する | 🟡 |
| `OB0023` | reset_settings | OneTime | 1 | g20 | 설정 초기화해보기 | Resetting to Default Settings | 設定を初期化してみる | 🟡 |
| `OB0024` | register_api_key | OneTime | 1 | g50, i1×1 | 나만의 API 키 등록하기 | Registering Your Own API Key | 自分のAPIキーを登録する | 🟡 |
| `OB0025` | installer_already_running | OneTime | 1 | g20 | 설치 프로그램 중복 실행 발견 | Catching a Duplicate Installer Run | 実行中のインストーラーを見つける | 🟢 |
| `OB0026` | curious_clicker | Increment | 5N | g10 | 아직 준비 안된 기능 발견하기 | Discovering Features Not Ready Yet | まだ準備中の機能を見つける | 🟠 |

_주요 훅: FirstRunManager.Start() — SettingManager.Instance.GetInstallStatus()==0 분기 진입 시 Report · ChatHandler.HandleInputSubmit() / HandleInputSubmitButton() / HandleInputWebSubmitButton() — 각 진입점에서 GameManager.Instance.chatIdx+=1 직후 Report · ClickHandler.HandleLeftClick() 정상 대화 분기 → ChatBalloonManager.Instance.ToggleChatBalloon() 호출 시 Report · ChangeCharListSlotController.ChangeChar() 및 ChangeCharCardController 동일 메서드 — 양쪽 모두에 Report · SettingManager.SetUiLanguage() 호출 직후 Report · DragHandler.PatHead() 최초 호출 시 ReportFlag · UIManager.showSettings() 및 ToggleSettings() 내 열림 분기 모두에 Report · AccessoryManager.Equip(GameObject target, string accessoryName, ...) 호출 시 Report · JukeboxView.Show() 호출 시 Report · ClickHandler.HandleLeftClick() → InstallerManager.IsJarvisServerInstalled()==false 시 ScenarioInstallerManager.StartInstaller() 호출 지점에 Report_

### CV — 대화 · 신규 58개 (기존 9개에 이어 CV0010부터)

| id | name | type | 목표 | 보상 | 한국어 | English | 日本語 | 연동 |
|----|------|------|------|------|--------|---------|--------|------|
| `CV0010` | first_conversation | OneTime | 1 | g50 | 첫 대화 | First Conversation | はじめての会話 | 🟠 |
| `CV0011` | total_conversations | Tiered | 50 / 300 / 1000 | g100 / g300 / g800 | 대화의 발자취 | Conversation Milestones | 会話の軌跡 | 🟠 |
| `CV0012` | emotion_diversity | Tiered | 3 / 5 / 6 | g50 / g100 / i1×1 | 감정의 스펙트럼 | Spectrum of Emotions | 感情のスペクトル | 🟡 |
| `CV0013` | thinking_response_count | Increment | 10N | g20 | 깊은 생각 | Deep in Thought | 深い思考 | 🟢 |
| `CV0014` | web_search_use_count | Increment | 10N | g20 | 궁금한 건 찾아봐요 | Web Explorer | 気になることは検索 | 🟢 |
| `CV0015` | web_search_forced_use | Increment | 5N | g30 | 직접 검색 요청 | Search On Demand | 検索リクエスト | 🟢 |
| `CV0016` | image_attach_use | Increment | 15N | g25 | 사진으로 대화하기 | Picture Talk | 写真でおしゃべり | 🟢 |
| `CV0017` | image_source_diversity | OneTime | 1 | g50 | 클립보드와 스크린샷 둘 다 | Both Ways to Share | クリップボードもスクショも | 🟢 |
| `CV0018` | hidden_image_preview | OneTime | 1 | g30 | 숨겨진 미리보기 | Hidden Preview | 隠されたプレビュー | 🟢 |
| `CV0019` | chat_regenerate_use | Increment | 8N | g20 | 다시 한번 들어볼래 | Ask Again | もう一度聞いてみる | 🟡 |
| `CV0020` | delete_recent_dialogue | OneTime | 1 | g30 | 되돌리기 | Second Thoughts | 巻き戻し | 🟢 |
| `CV0021` | language_switch_all | OneTime | 1 | g40 | 세 가지 언어로 | Trilingual Chat | 三か国語で会話 | 🟢 |
| `CV0022` | ai_choice_click | Increment | 10N | g20 | AI의 추천을 따라서 | Following AI's Lead | AIのおすすめに従って | 🟢 |
| `CV0023` | character_diversity | Tiered | 2 / 3 / 4 | g50 / g150 / g300 | 다양한 얼굴들과의 대화 | Many Faces, Many Talks | いろんな顔とおしゃべり | 🟡 |
| `CV0024` | minigame_20q_play | Increment | 5N | g40 | 스무고개 도전 | Twenty Questions Challenge | 二十の扉チャレンジ | 🟠 |
| `CV0025` | late_night_chat | OneTime | 1 | g30 | 늦은 밤의 대화 | Late Night Talk | 夜更けのおしゃべり | 🟠 |
| `CV0026` | furigana_use | OneTime | 1 | g30 | 후리가나의 비밀 | Furigana Secret | ふりがなの秘密 | 🟢 |
| `CV0027` | translate_test_use | OneTime | 1 | g30 | 세 언어로 한 번에 | Triple Translate | 三言語同時翻訳 | 🟢 |
| `CV0028` | first_choice_click | OneTime | 1 | g50 | 첫 선택의 순간 | First Choice | はじめての選択 | 🟡 |
| `CV0029` | choice_click_count | Increment | 10N | g10 | 선택의 달인 | Choice Maker | 選択の達人 | 🟡 |
| `CV0030` | scenario_variety | Tiered | 2 / 3 / 4 | g80 / g150 / g250, i1×1 | 다양한 길 걷기 | Path Explorer | いろんな道を歩く | 🟠 |
| `CV0031` | ai_choice_first | OneTime | 1 | g50 | AI의 제안을 받아들이다 | Trusting AI | AIの提案を受け入れる | 🟡 |
| `CV0032` | ai_choice_count | Increment | 10N | g15 | AI와 함께 대화하기 | Talking with AI | AIと話す | 🟡 |
| `CV0033` | ai_choice_streak | Tiered | 3 / 5 / 10 | g100 / g200 / g400, i1×1 | AI에게 맡겨보기 | AI Autopilot | AIにおまかせ | 🟠 |
| `CV0034` | ai_choice_ignore | OneTime | 1 | g60 | 내 생각대로 말하기 | Speaking My Mind | 自分の言葉で | 🟠 |
| `CV0035` | ai_choice_dismiss | OneTime | 1 | g50 | 제안은 넣어둘게 | Not Now, AI | 今はいいかな | 🟠 |
| `CV0036` | ai_choice_recall | OneTime | 1 | g30 | 다시 볼래요 | One More Look | もう一度見る | 🟡 |
| `CV0037` | tutorial_first_choice | OneTime | 1 | g50 | 첫 발걸음 | First Step | 最初の一歩 | 🟡 |
| `CV0038` | onboarding_tech_path | Tiered | 1 / 2 | g60 / g120 | 내게 맞는 방식 찾기 | Finding My Setup | 自分に合う設定 | 🟠 |
| `CV0039` | server_key_choice | OneTime | 1 | g50 | 서버와 키 사이 | Server or Key | サーバーか鍵か | 🟡 |
| `CV0040` | api_key_verified | Tiered | 1 / 2 | g80 / g180, i1×1 | 열쇠가 맞았어요 | Key Verified | 鍵が合いました | 🟠 |
| `CV0041` | edition_choice | Tiered | 1 / 2 | g60 / g140, i1×1 | 에디션 고르기 | Choosing an Edition | エディション選び | 🟠 |
| `CV0042` | key_exhausted_response | OneTime | 1 | g50 | 다음 방법 찾기 | Plan B | 次の一手 | 🟡 |
| `CV0043` | mic_retry_confirm | OneTime | 1 | g30 | 마이크야 들리니 | Mic Check | マイク聞こえる? | 🟡 |
| `CV0044` | multimodal_switch | Tiered | 1 / 2 | g50 / g100 | 눈을 뜨다 | Opening Its Eyes | 目を開く | 🟠 |
| `CV0045` | cancel_and_retry | OneTime | 1 | g200, i2×1 | 다시 도전하는 마음 | Try Again | もう一度チャレンジ | 🔴 |
| `CV0046` | first_hello | OneTime | 1 | g50 | 첫 인사 | First Hello | はじめてのあいさつ | 🟠 |
| `CV0047` | greet_the_change | Increment | 10N | g10 | 다시 만나 인사하기 | Say Hello Again | また会えたね | 🟡 |
| `CV0048` | outfit_swap | Tiered | 5 / 20 / 50 | g50 / g150 / g300 | 옷 갈아입히기 | Outfit Change | きせかえ | 🟡 |
| `CV0049` | costume_collector | OneTime | 1 | g100, i1×1 | 코스튬 컬렉터 | Costume Collector | コスチュームコレクター | 🟠 |
| `CV0050` | summon_friend | Tiered | 5 / 20 / 50 | g50 / g150 / g300 | 친구 소환하기 | Summon a Friend | フレンドを呼ぶ | 🟡 |
| `CV0051` | summon_variety | OneTime | 5 | g80, i1×1 | 모여라 친구들 | Meet the Gang | いろんな仲間 | 🟠 |
| `CV0052` | gentle_poke | Increment | 20N | g10 | 콕콕 찔러보기 | Poke Poke | つんつん | 🟡 |
| `CV0053` | pick_me_up | Tiered | 10 / 50 / 150 | g50 / g150 / g300 | 안아 올리기 | Pick Me Up | 抱っこして | 🟡 |
| `CV0054` | gentle_pat_first | OneTime | 1 | g60 | 첫 쓰다듬기 | First Pat | はじめてのなでなで | 🟡 |
| `CV0055` | pat_lover | Increment | 10N | g10 | 쓰다듬기 애호가 | Head Pat Enthusiast | なでなで愛好家 | 🟡 |
| `CV0056` | arona_special_face | OneTime | 1 | g80, i2×1 | 아로나의 비밀 표정 | Arona's Secret Face | アロナの秘密の表情 | 🟡 |
| `CV0057` | voice_panel_peek | OneTime | 1 | g40 | 음성 패널 열어보기 | Open the Voice Panel | ボイスパネルを開く | 🟡 |
| `CV0058` | special_expression | OneTime | 1 | g70 | 깜짝 애니메이션 | Surprise Animation | サプライズアニメ | 🟠 |
| `CV0059` | random_motion_variety | OneTime | 4 | g90, i1×1 | 네 가지 몸짓 | Four Moves | よっつのモーション | 🟠 |
| `CV0060` | daily_chat_streak | Tiered | 3 / 7 / 21 | g80 / g200 / g500 | 날마다 이어지는 수다 | Every Day We Talk | 毎日続くおしゃべり | 🟡 |
| `CV0061` | morning_greeting_streak | Tiered | 3 / 7 / 14 | g50 / g150 / g400 | 아침마다 첫 인사 | Good Morning Streak | 毎朝のあいさつ | 🟡 |
| `CV0062` | night_chat_streak | Tiered | 3 / 7 / 14 | g60 / g150 / g350 | 밤마다 나누는 대화 | Nightly Talks | 毎晩のおしゃべり | 🟠 |
| `CV0063` | emotional_choice_moment | OneTime | 1 | g50 | 선택이 부른 감정 | Choice-Sparked Emotion | 選択が呼んだ感情 | 🟢 |
| `CV0064` | morning_greeting | OneTime | 1 | g50 | 아침 인사 나누기 | Good Morning | おはようの挨拶 | 🟢 |
| `CV0065` | all_day_companion | Tiered | 2 / 3 / 4 | g80 / g150 / g300 | 하루의 모든 시간과 함께 | Around the Clock | 一日中いっしょ | 🟡 |
| `CV0066` | new_year_greeting | OneTime | 1 | g200 | 새해 첫 인사 | New Year's Greeting | 新年のあいさつ | 🟠 |
| `CV0067` | copy_response_share | Increment | 5N | g30 | 아이코의 말 복사해 공유하기 | Copy AICO's reply to share | アイコの返事をコピーして共有 | 🟢 |

_주요 훅: APIManager.OnFinalResponseReceived — 대화가 완결(final reply 수신)되는 시점에 신규 영구 플래그(예: config/conversation.json)로 최초 1회 여부 판별 후 ReportFlag · APIManager.OnFinalResponseReceived — GlobalTimeVariableManager.totalPlaySeconds(config/time.json) 패턴을 복제한 신규 영구 대화 누적 카운터에 매 final 응답 수신 시 Report/ReportBest · EmotionManager.ShowEmotionFromEmotion 호출 지점 3곳(APIManager.FetchStreamingData, PrepareConversationReplyUiFromRouter, CallSmallTalkStream)에서 수신한 emotion 문자열을 Set<string>에 누적, distinct count를 ReportBest · FetchStreamingData / ProcessConversationStreamEventFromRouter / CallSmallTalkStream / CallMiniGame20QStream 내 replyType=="thinking" 분기(→NoticeManager.Notice("thinking")) 4곳에 Report 훅 추가 · 위 4곳의 replyType=="webSearch" 분기(→NoticeManager.Notice("webSearch"))에 Report 훅 추가 · ChatHandler.HandleInputWebSubmitButton → GameManager.isWebSearchForced=true 설정 직후 Report · APIManager.CallConversationStream 내 intent_image가 "auto"/"force"로 확정되어 AttachImageToRequest가 실제 호출되는 지점에 Report · ChatBalloonManager.GetImageSource()가 반환하는 "clipboard"/"screenshot" 값을 Set으로 추적, 둘 다 등장 시 ReportFlag · ChatBalloonManager.SetupImageUseBtnRightClick → ShowCurrentImage 호출 시 ReportFlag · AnswerBalloonManager.ChatRegenerate 호출 시 신규 영구 누적 카운터에 Report_

### AF — 교감 · 신규 87개 (기존 5개에 이어 AF0006부터)

| id | name | type | 목표 | 보상 | 한국어 | English | 日本語 | 연동 |
|----|------|------|------|------|--------|---------|--------|------|
| `AF0006` | emotion_first_reaction | OneTime | 1 | g50 | 첫 감정의 순간 | First Spark of Emotion | はじめての感情 | 🟡 |
| `AF0007` | emotion_diversity_complete | OneTime | 6 | g150, i1×1 | 감정 도감 완성 | Emotion Collector | 感情図鑑コンプリート | 🟠 |
| `AF0008` | emotion_joy_cumulative | Tiered | 10 / 50 / 150 | g50 / g150 / g400 | 웃음이 번지는 하루 | Spreading Joy | 広がる笑顔 | 🟠 |
| `AF0009` | emotion_streak | Tiered | 3 / 5 / 10 | g100 / g250 / g600 | 한마음 감정 연속 | Emotional Streak | 感情連続コンボ | 🟠 |
| `AF0010` | pat_head_first | OneTime | 1 | g50 | 포근한 첫 손길 | A Gentle First Touch | はじめてのふれあい | 🟡 |
| `AF0011` | pat_head_cumulative | Increment | 10N | g20 | 매일매일 쓰다듬기 | Daily Pats | 毎日のふれあい | 🟡 |
| `AF0012` | arona_heart_eyes | OneTime | 1 | g80, i1×1 | 아로나의 하트눈 | Arona's Heart Eyes | アロナのハート目 | 🟡 |
| `AF0013` | listen_expression_first | OneTime | 1 | g50 | 귀 기울이는 순간 | Lending an Ear | 耳を傾ける瞬間 | 🟡 |
| `AF0014` | listen_expression_cumulative | Increment | 10N | g20 | 언제나 듣고 있어요 | Always Listening | いつも聞いています | 🟡 |
| `AF0015` | mic_listen_notice_first | OneTime | 1 | g50 | 목소리를 담는 순간 | Capturing Your Voice | 声をキャッチ | 🟡 |
| `AF0016` | stt_write_notice_first | OneTime | 1 | g50 | 말을 글로 옮기는 중 | Turning Words into Text | 言葉を文字に | 🟡 |
| `AF0017` | vl_agent_success_first | OneTime | 1 | g80 | 첫 성공의 끄덕임 | First Nod of Success | はじめての成功 | 🟡 |
| `AF0018` | vl_agent_success_cumulative | Tiered | 10 / 30 / 100 | g150 / g350 / g800 | 믿음직한 손길 | A Reliable Helper | 頼れる相棒 | 🟠 |
| `AF0019` | vl_agent_comeback | OneTime | 1 | g120, i2×1 | 실패를 딛고 다시 | Rising After a Stumble | 失敗を乗り越えて | 🟠 |
| `AF0020` | aropla_speaker_balloon_first | OneTime | 1 | g50 | 다음 이야기를 준비하며 | Who Speaks Next? | 次に話すのは？ | 🟡 |
| `AF0021` | aropla_speaker_diversity | OneTime | 2 | g100 | 아로나와 프라나, 둘 다 | Both Arona & Plana | アロナとプラナ、両方 | 🟠 |
| `AF0022` | operator_portrait_balloon_first | OneTime | 1 | g50 | 오퍼레이터의 표정 | The Operator's Expression | オペレーターの表情 | 🟡 |
| `AF0023` | character_emotion_diversity | OneTime | 3 | g150, i1×1 | 세 얼굴의 감정 | Three Faces, One Heart | 三人の表情 | 🟠 |
| `AF0024` | first_headpat | OneTime | 1 | g50 | 첫 쓰다듬 | First Pat | はじめてのなでなで | 🟠 |
| `AF0025` | headpat_streak | Tiered | 10 / 50 / 100 | g50 / g150 / g400 | 쓰담쓰담 단골 | Petting Regular | なでなで常連 | 🟠 |
| `AF0026` | headpat_friends | Tiered | 3 / 6 / 10 | g80 / g200 / g450 | 다정한 손길 | Gentle Hands | やさしい手つき | 🟠 |
| `AF0027` | arona_blush | OneTime | 1 | g80, i1×1 | 아로나의 두근거림 | Arona's Blush | アロナの胸キュン | 🟠 |
| `AF0028` | sub_headpat_dream | OneTime | 1 | g300, i2×1 | 작은 아이의 꿈 | Little One's Dream | 小さな子の夢 | 🔴 |
| `AF0029` | first_pickup | OneTime | 1 | g50 | 처음 안아보기 | First Pick-Up | はじめてのだっこ | 🟠 |
| `AF0030` | pickup_lover | Increment | 10N | g20 | 품 안의 단골 | Always in Your Arms | いつも抱っこ | 🟠 |
| `AF0031` | sub_first_pickup | OneTime | 1 | g50 | 작은 친구 안아주기 | Hug the Little Friend | 小さな仲間をだっこ | 🟠 |
| `AF0032` | gentle_hold | Tiered | 3 / 10 / 30 | g50 / g150 / g350 | 오래 안아주기 | Long Embrace | 長いハグ | 🟠 |
| `AF0033` | sub_poke_reactions | Tiered | 2 / 3 / 4 | g60 / g150 / g300 | 콕콕 반응 모음 | Poke Reaction Collector | つんつん反応コレクター | 🟠 |
| `AF0034` | wake_up_chat | Increment | 10N | g15 | 말 걸어보기 | Say Hello | 話しかけてみよう | 🟠 |
| `AF0035` | first_portrait_touch | OneTime | 1 | g50 | 초상화 톡톡 | Portrait Tap | 肖像タッチ | 🟠 |
| `AF0036` | pixel_perfect | Increment | 50N | g10 | 정확히 맞추기 | Pixel Perfect Touch | ピクセルパーフェクト | 🟡 |
| `AF0037` | double_tap_greeting | OneTime | 1 | g50 | 두 번의 인사 | Double Tap Hello | ダブルタップの挨拶 | 🟠 |
| `AF0038` | gentle_drop | OneTime | 1 | g50 | 살짝 놓아주기 | Gentle Drop | そっと手放す | 🟠 |
| `AF0039` | drop_repeat | Increment | 10N | g20 | 낙하 놀이 | Drop Play | 落下あそび | 🟠 |
| `AF0040` | gravity_awakening | OneTime | 1 | g30 | 중력을 깨우다 | Awaken Gravity | 重力を起こす | 🟡 |
| `AF0041` | sub_resize_extreme | Tiered | 50 / 100 / 200 | g50 / g100 / g250 | 크기 탐험가 | Size Explorer | サイズ探検家 | 🟠 |
| `AF0042` | char_change_tiered | Tiered | 5 / 25 / 50 | g100 / g300, i1×1 / g600, i2×1 | 갈아입히는 재미 | The Joy of Change | 着せ替えの楽しみ | 🟡 |
| `AF0043` | char_change_habit | Increment | 20N | g50/lv | 오늘도 새로운 얼굴 | A New Face Today | 今日も新しい顔 | 🟡 |
| `AF0044` | char_diversity_collector | Tiered | 3 / 7 / 12 | g150 / g350, i1×1 / g700, i2×1 | 취향은 넓게 | Wide Taste | 好みは広く | 🟠 |
| `AF0045` | outfit_swap_first | OneTime | 1 | g80 | 처음 갈아입기 | First Outfit Change | はじめての着替え | 🟡 |
| `AF0046` | outfit_swap_habit | Increment | 15N | g40/lv | 옷장 순례 | Wardrobe Pilgrimage | 衣装めぐり | 🟡 |
| `AF0047` | costume_rotate_first | OneTime | 1 | g80 | 코스튬 첫 회전 | First Costume Spin | はじめてのコスチューム | 🟡 |
| `AF0048` | costume_full_tour | OneTime | 1 | g250, i1×1 | 코스튬 한 바퀴 | Full Costume Tour | コスチューム一巡 | 🟠 |
| `AF0049` | char_size_tune | OneTime | 1 | g60 | 크기 맞추기 | Just the Right Size | ぴったりサイズ | 🟡 |
| `AF0050` | char_size_extreme | OneTime | 2 | g120 | 최대와 최소 사이 | Between Max and Min | 最大と最小のあいだ | 🟡 |
| `AF0051` | favorite_first | OneTime | 1 | g60 | 첫 즐겨찾기 | First Favorite | はじめてのお気に入り | 🟡 |
| `AF0052` | favorite_collector | Tiered | 3 / 10 / 20 | g100 / g280, i1×1 / g550, i2×1 | 마음에 든 아이들 | My Favorites | お気に入りの子たち | 🟡 |
| `AF0053` | favorite_filter_first | OneTime | 1 | g50 | 즐겨찾기만 보기 | Favorites Only | お気に入りだけ見る | 🟡 |
| `AF0054` | view_mode_switch | OneTime | 1 | g40 | 보기 방식 바꿔보기 | Try a New View | 表示スタイルを変える | 🟡 |
| `AF0055` | card_outfit_browse | OneTime | 1 | g50 | 카드 넘겨보기 | Flip the Card | カードをめくる | 🟡 |
| `AF0056` | detail_view_first | OneTime | 1 | g60 | 첫 상세정보 | First Look | はじめてのプロフィール | 🟡 |
| `AF0057` | detail_view_diversity | Tiered | 3 / 8 / 15 | g150 / g350, i1×1 / g700, i2×1 | 모두 알아가기 | Getting to Know Everyone | みんなを知る | 🟠 |
| `AF0058` | dlc_download_first | OneTime | 1 | g100 | 새 친구 데려오기 | Bringing Someone New | 新しい仲間を迎える | 🟡 |
| `AF0059` | dlc_download_collector | Tiered | 3 / 10 / 20 | g200 / g450, i1×1 / g900, i2×1 | 늘어나는 옷장 | Growing Collection | 増えていくコレクション | 🟡 |
| `AF0060` | pat_head_streak | Increment | 10N | g10 | 매일 쓰다듬기 | Pat by Pat | なでなでの積み重ね | 🟡 |
| `AF0061` | pat_head_master | Tiered | 50 / 200 / 500 | g100 / g250 / g500 | 쓰다듬기 달인 | Petting Master | なでなでマスター | 🟠 |
| `AF0062` | change_character_first | OneTime | 1 | g30 | 다른 아이 만나보기 | Meet Someone New | 別の子と会ってみる | 🟡 |
| `AF0063` | change_character_explorer | Increment | 10N | g15 | 여러 아이들과 어울리기 | Circle of Friends | いろんな子と交流 | 🟡 |
| `AF0064` | favorite_character_first | OneTime | 1 | g40 | 마음에 드는 아이 등록하기 | My Favorite | お気に入り登録 | 🟡 |
| `AF0065` | poke_character_curious | Increment | 15N | g15 | 콕콕 장난치기 | Just a Little Poke | つんつんしてみる | 🟠 |
| `AF0066` | emotion_collector | OneTime | 6 | g200 / i1x1 | 모든 표정 만나보기 | Every Expression | すべての表情を見る | 🟠 |
| `AF0067` | meet_many_friends | Tiered | 2 / 4 / 6 | g150 / g300 / g500 | 친구가 늘어나요 | Growing Circle | 広がる交友関係 | 🟠 |
| `AF0068` | affinity_level_up | Increment | 2N+1 | g25 | 인연도 레벨업 | Bond Level Up | きずなレベルアップ | 🟠 |
| `AF0069` | affinity_tier_familiar | OneTime | 1 | g300 | 친근한 사이가 되다 | Becoming Familiar | 親しい仲になる | 🔴 |
| `AF0070` | affinity_tier_close | OneTime | 1 | g600 | 마음을 나누는 사이 | Growing Closer | 心を通わせる仲 | 🔴 |
| `AF0071` | affinity_tier_max | OneTime | 1 | g1000 / i2x1 | 최고의 인연 | Bond at Its Fullest | 最高のきずな | 🔴 |
| `AF0072` | affinity_circle_wide | Tiered | 2 / 4 / 6 | g400 / g800 / g1500 / i3x1 | 여러 마음과 이어지기 | Many Bonds, One Heart | たくさんの絆 | 🔴 |
| `AF0073` | resize_character_first | OneTime | 1 | g80 | 딱 맞는 크기로 | Just the Right Size | ぴったりサイズ | 🟠 |
| `AF0074` | af_style_swap_debut | OneTime | 1 | g80 | 첫 코디 체인지 | First Style Swap | はじめてのスタイル替え | 🟡 |
| `AF0075` | af_wardrobe_climber | Tiered | 10 / 30 / 60 | g100 / g250 / g450 | 옷장 정복기 | Wardrobe Climber | ワードローブ制覇 | 🟡 |
| `AF0076` | af_daily_dressup | Increment | 20N | g40 | 매일 코디하기 | Daily Dress-Up | 毎日おめかし | 🟡 |
| `AF0077` | af_tidy_habit | Increment | 10N | g30 | 깔끔한 정리 습관 | Tidy-Up Habit | きれい好き習慣 | 🟡 |
| `AF0078` | af_style_switcher | Tiered | 5 / 15 / 30 | g80 / g200 / g380 | 스타일 체인지 마스터 | Style Switch Master | スタイル切り替えマスター | 🟡 |
| `AF0079` | af_fashion_explorer | Tiered | 3 / 6 / 10 | g120 / g280 / g500, i1×1 | 패션 탐험가 | Fashion Explorer | ファッション探検家 | 🟠 |
| `AF0080` | af_slot_explorer | Tiered | 2 / 4 / 6 | g100 / g220 / g400 | 다재다능 코디 | All-Around Stylist | オールラウンドスタイリスト | 🟠 |
| `AF0081` | af_squad_stylist | OneTime | 3 | g200, i1×1 | 우리 모두 꾸미기 | Dress the Whole Crew | みんなをおしゃれに | 🟠 |
| `AF0082` | af_accessory_shopper | Tiered | 5 / 15 / 30 | g150 / g300 / g500 | 악세서리 쇼퍼 | Accessory Shopper | アクセサリーショッパー | 🔴 |
| `AF0083` | af_accessory_hoarder | Tiered | 3 / 10 / 20 | g200 / g400 / g700, i2×1 | 악세서리 수집가 | Accessory Collector | アクセサリーコレクター | 🔴 |
| `AF0084` | dressup_snapshot | OneTime | 1 | g60 | 새 옷 인증샷 | New Outfit Snapshot | 着替えの記念ショット | 🟢 |
| `AF0085` | affinity_max_collector | Tiered | 2 / 3 / 5 | g300 / g500 / i3×1 | 모두와 최고의 인연 | Best Bonds with Many | みんなと最高の絆 | 🔴 |
| `AF0086` | costume_grand_collection | OneTime | 1 | g600, i3×1 | 모든 옷을 입어본 날 | Every Costume Worn | 全コスチューム制覇 | 🔴 |
| `AF0087` | pickup_all_characters | Tiered | 2 / 4 / 6 | g100 / g250 / g500 | 모두 안아보기 | Hold Everyone | みんなを抱っこ | 🟠 |
| `AF0088` | scroll_wheel_resize | OneTime | 1 | g50 | 스크롤로 크기 바꾸기 | Resize with the Scroll Wheel | スクロールでサイズ変更 | 🟢 |
| `AF0089` | showcase_snapshot_first | OneTime | 1 | g50 | 꾸민 아이코를 사진으로 남기기 | Snapshot your styled AICO | 着せ替えたアイコを写真に残す | 🟢 |
| `AF0090` | showcase_snapshot_collector | Tiered | 5 / 15 / 30 | g100 / g200 / g400 | 스크린샷 갤러리 채우기 | Fill your snapshot gallery | スクショギャラリーを埋める | 🟡 |
| `AF0091` | total_makeover | OneTime | 1 | g150, i1×1 | 완벽한 변신 | Total makeover | 完全なる変身 | 🟡 |
| `AF0092` | screenshot_clipboard_share | OneTime | 1 | g50 | 사진 복사해서 자랑하기 | Copy your snapshot to share | スクショをコピーして自慢する | 🟢 |

_주요 훅: APIManager.cs L335/L1185/L1782 → EmotionManager.ShowEmotionFromEmotion(ai_info_emotion) 최초 호출 시 Report · EmotionManager.ShowEmotionFromEmotion 콜사이트 근처 신규 HashSet<string> seenEmotions 추적 → 6종 도달 시 Report · EmotionManager.ShowEmotionFromEmotion 콜사이트에 신규 per-emotion(Joy) 카운터 증가 시 ReportFlag/Report · EmotionManager.ShowEmotionFromEmotion 콜사이트에 신규 lastEmotion 필드+streak 카운터 추가 후 값 갱신 시 ReportBest · DragHandler.PatHead() → EmotionBalloonManager.ShowEmotionBalloon(this.gameObject, "Love") 최초 호출 시 Report · DragHandler.PatHead() 호출 시점에 카운터 증가 → 10회 단위 Report · DragHandler.PatHead() 내 nickname=="arona" 분기 → EmotionManager.ShowEmotion("><") 최초 호출 시 Report · ChatBalloonManager.cs L290/294, SubChatBalloonController.cs L131 → AnimationManager.Listen()→EmotionManager.ShowEmotionFromAction("listen") 최초 호출 시 Report · AnimationManager.Listen() 호출 지점에 카운터 증가 → 10회 단위 Report · MicrophoneManager.cs L105 → NoticeManager.ShowNoticeEmotionBalloon("Listen", ...) 최초 호출 시 Report_

### PR — 생활 · 신규 88개 (기존 8개에 이어 PR0009부터)

| id | name | type | 목표 | 보상 | 한국어 | English | 日本語 | 연동 |
|----|------|------|------|------|--------|---------|--------|------|
| `PR0009` | alarm_create_count | Tiered | 1 / 5 / 10 | g50 / g150 / g300, i1×1 | 알람 만들기 | Set an Alarm | アラームを設定 | 🟡 |
| `PR0010` | timer_first_use | OneTime | 1 | g80 | 첫 타이머 사용 | First Timer | はじめてのタイマー | 🟡 |
| `PR0011` | alarm_ring_count | Increment | 3N | g15 | 알람아 울려라 | Alarm Calling | アラームが鳴る | 🟢 |
| `PR0012` | alarm_delete_count | Increment | 10N | g10 | 알람 정리하기 | Clean Up Alarms | アラーム整理 | 🟡 |
| `PR0013` | alarm_title_customize | OneTime | 1 | g60 | 알람에 이름 붙이기 | Name Your Alarm | アラームに名前を | 🟡 |
| `PR0014` | alarm_toggle_count | Increment | 10N | g10 | 알람 켜고 끄기 | Flip the Switch | アラームON/OFF | 🟡 |
| `PR0015` | weekday_repeat_setup | OneTime | 1 | g120, i1×1 | 요일 반복 설정 | Repeat on Weekdays | 曜日リピート設定 | 🟠 |
| `PR0016` | exclude_weekend_setup | OneTime | 1 | g80 | 주말은 쉬어가기 | Weekend Off | 週末はお休み | 🟡 |
| `PR0017` | character_voice_select | OneTime | 1 | g100, i2×1 | 캐릭터 음성 선택 | Choose a Voice | キャラの声を選ぶ | 🟠 |
| `PR0018` | timer_start_count | Increment | 10N | g15 | 타이머 시작하기 | Start the Clock | タイマー開始 | 🟡 |
| `PR0019` | timer_pause_count | Increment | 10N | g10 | 잠깐 멈춤 | Pause Break | ちょっと一息 | 🟡 |
| `PR0020` | timer_reset_count | Increment | 10N | g10 | 타이머 리셋 | Reset & Retry | タイマーリセット | 🟡 |
| `PR0021` | mini_widget_open | OneTime | 1 | g70 | 미니 위젯 열기 | Open Mini Timer | ミニウィジェットを開く | 🟡 |
| `PR0022` | alarm_panel_open | OneTime | 1 | g30 | 알람 화면 첫 방문 | Visit the Alarm Screen | アラーム画面へ | 🟡 |
| `PR0023` | dual_type_master | OneTime | 1 | g150, i1×1 | 알람과 타이머 모두 | Alarm & Timer Duo | アラームとタイマー両方 | 🟠 |
| `PR0024` | concurrent_active_tier | Tiered | 2 / 3 / 5 | g100 / g200 / g400, i2×1 | 동시에 여러 개 | Multitasking Time | 同時に複数稼働 | 🟠 |
| `PR0025` | timer_no_pause_complete | OneTime | 1 | g120, i2×1 | 멈추지 않고 완주 | No-Pause Finish | 止まらず完走 | 🟠 |
| `PR0026` | early_morning_alarm | OneTime | 1 | g100, i1×1 | 일찍 일어나는 새 | Early Bird Alarm | 早起きアラーム | 🟠 |
| `PR0027` | pro_timer_use | OneTime | 1 | g50 | 타이머 사용해보기 | Use a timer | タイマーを使ってみる | 🟡 |
| `PR0028` | pro_pomodoro_complete | Tiered | 1 / 5 / 20 | g80 / g200, i1×1 / g500 | 포모도로 완료하기 | Complete pomodoro sessions | ポモドーロを完了する | 🟡 |
| `PR0029` | pro_pomodoro_full_set | Tiered | 1 / 10 / 30 | g100 / g300, i1×1 / g700, i2×1 | 오늘 집중 끝내기 | Finish a full focus set | 今日の集中をやり切る | 🟠 |
| `PR0030` | pro_break_complete | Increment | 10N | g30 | 쉬는 시간 잘 챙기기 | Take your breaks | しっかり休憩する | 🟠 |
| `PR0031` | pro_custom_focus_time | OneTime | 1 | g60 | 내 시간표로 맞추기 | Set your own timer | 自分だけの時間に設定する | 🟠 |
| `PR0032` | pro_boost_first_use | OneTime | 1 | g40 | 급속 버튼 눌러보기 | Try the boost button | ブーストボタンを試す | 🟠 |
| `PR0033` | pro_boost_max_speed | OneTime | 1 | g110 | 최고 배속 찍어보기 | Hit max boost speed | 最大速度に到達する | 🟠 |
| `PR0034` | pro_boost_marathon | Increment | 60N | g25 | 급속 마라토너 | Boost marathon | ブーストマラソン | 🟠 |
| `PR0035` | pro_widget_compact_view | OneTime | 1 | g20 | 작게 모아두기 | Go compact | コンパクトにまとめる | 🔴 |
| `PR0036` | pro_widget_full_return | OneTime | 1 | g20 | 다시 크게 펼치기 | Back to full view | フル表示に戻す | 🔴 |
| `PR0037` | pro_widget_reposition | OneTime | 1 | g25 | 내 자리 찾아주기 | Move your timer | タイマーを移動する | 🔴 |
| `PR0038` | pro_boost_pause_skip | OneTime | 1 | g90 | 일시정지 중 급속 스킵 | Skip while paused, boosted | 一時停止中にブーストでスキップ | 🔴 |
| `PR0039` | pro_focus_without_reset | OneTime | 1 | g150 | 끝까지 리셋 없이 | No resets, full focus | リセットなしで完走 | 🔴 |
| `PR0040` | pro_pure_focus | Tiered | 1 / 10 / 30 | g90 / g250, i1×1 / g600, i2×1 | 부스트 없이 순수 집중 | Pure focus, no boost | ブーストなしの純粋集中 | 🟠 |
| `PR0041` | todo_open_list | OneTime | 1 | g50 | 첫 할일 목록 | First To-Do Peek | 初めてのTo-Doリスト | 🟡 |
| `PR0042` | todo_first_check | OneTime | 1 | g50 | 첫 완료의 기쁨 | First Checkmark | はじめての完了 | 🟡 |
| `PR0043` | todo_planner | Tiered | 10 / 30 / 60 | g100 / g250 / g450 | 계획의 달인 | Planner in Progress | 計画の達人 | 🟡 |
| `PR0044` | todo_checker | Increment | 10N | g30 | 체크의 손맛 | Checkbox Rhythm | チェックの心地 | 🟡 |
| `PR0045` | todo_perfect_day | Tiered | 1 / 5 / 15 | g150 / g400 / g800 | 완벽한 하루 | Perfect Day Clear | 完璧な一日 | 🟠 |
| `PR0046` | todo_streak | Tiered | 3 / 7 / 14 | g300 / g700 / g1500, i2×1 | 연속 완주 | Streak Keeper | 連続クリア | 🔴 |
| `PR0047` | todo_cleaner | OneTime | 1 | g50 | 정리의 시작 | First Cleanup | はじめての整理 | 🟠 |
| `PR0048` | todo_editor | Increment | 2N+1 | g25 | 다듬는 손길 | Fine-Tuner | 書き直しの手 | 🟠 |
| `PR0049` | todo_organizer | OneTime | 1 | g60 | 순서의 재발견 | Reorder Discovery | 並び替え発見 | 🟡 |
| `PR0050` | todo_explorer | Tiered | 3 / 7 / 14 | g80 / g200 / g400 | 날짜 탐험가 | Date Explorer | 日付の探検家 | 🟠 |
| `PR0051` | todo_time_master | OneTime | 1 | g100, i1×1 | 시간의 비밀 | Hidden Time Trick | 時間の秘密 | 🟠 |
| `PR0052` | todo_delegate | Tiered | 1 / 5 / 15 | g100 / g300 / g600 | 믿고 맡기기 | Delegate to AICO | AICOにおまかせ | 🟡 |
| `PR0053` | todo_curious | Tiered | 5 / 20 / 50 | g60 / g180 / g400 | 궁금한 마음 | Curious Clicks | 気になる詳細 | 🟠 |
| `PR0054` | pr_calendar_open_first | OneTime | 1 | g30 | 첫 캘린더 열기 | Open the Calendar | はじめてのカレンダー | 🟡 |
| `PR0055` | pr_calendar_open_streak | Increment | 10N | g20 | 캘린더 습관 만들기 | Calendar Habit | カレンダー習慣 | 🟡 |
| `PR0056` | pr_calendar_pick_date_first | OneTime | 1 | g20 | 날짜 골라보기 | Pick a Date | 日付を選んでみる | 🟢 |
| `PR0057` | pr_calendar_pick_date_streak | Increment | 15N | g15 | 날짜 탐색가 | Date Explorer | 日付エクスプローラー | 🟢 |
| `PR0058` | pr_calendar_pick_variety | Tiered | 5 / 15 / 30 | g30 / g80 / g150 | 여러 날짜 둘러보기 | Calendar Wanderer | いろんな日をめぐる | 🟢 |
| `PR0059` | pr_calendar_pick_not_today | OneTime | 1 | g25 | 다른 날도 들여다보기 | Peek at Another Day | 別の日をのぞいてみる | 🟡 |
| `PR0060` | pr_calendar_navigate_month | Increment | 8N | g25 | 달력 넘겨보기 | Flip the Pages | カレンダーをめくる | 🟡 |
| `PR0061` | pr_calendar_month_variety | Tiered | 3 / 6 / 12 | g50 / g120 / g250, i1×1 | 열두 달 여행 | Twelve Months Journey | 十二ヶ月の旅 | 🟠 |
| `PR0062` | pr_calendar_collapse_widget | OneTime | 1 | g20 | 미니 위젯으로 접기 | Shrink to a Widget | ミニウィジェットに縮める | 🟡 |
| `PR0063` | pr_calendar_drag_widget | OneTime | 1 | g20 | 위젯 옮겨보기 | Move the Widget | ウィジェットを動かす | 🟡 |
| `PR0064` | pr_calendar_drag_panel | OneTime | 1 | g30 | 캘린더 패널 옮기기 | Reposition the Calendar | カレンダーパネルを移動 | 🟠 |
| `PR0065` | pr_todo_add_first | OneTime | 1 | g40 | 첫 할 일 등록 | Add Your First To-Do | 最初のToDoを登録 | 🟡 |
| `PR0066` | pr_calendar_add_schedule | OneTime | 1 | g40 | 캘린더에서 일정 남기기 | Schedule via Calendar | カレンダーから予定を登録 | 🟠 |
| `PR0067` | pr_todo_fill_day | Tiered | 3 / 5 / 10 | g30 / g70 / g150 | 하루 가득 채우기 | Pack the Day | 一日を予定でいっぱいに | 🟡 |
| `PR0068` | pr_todo_date_variety | Tiered | 5 / 15 / 30 | g40 / g100 / g200, i1×1 | 이곳저곳에 일정 남기기 | Scattered Planner | あちこちに予定を残す | 🟠 |
| `PR0069` | pr_todo_fill_week | OneTime | 1 | g100, i2×1 | 한 주 완전 정복 | Full Week Planned | 一週間を計画で埋める | 🟠 |
| `PR0070` | jukebox_first_play | OneTime | 1 | g50 | 첫 곡 재생하기 | Play Your First Song | 初めての一曲を再生する | 🟡 |
| `PR0071` | jukebox_track_select | Increment | 10N | g15 (매 레벨) | 원하는 곡 골라 듣기 | Pick Your Own Tracks | 好きな曲を選んで聴く | 🟡 |
| `PR0072` | jukebox_track_variety | Tiered | 5 / 15 / 30 | g100 / g250 / g500 | 다양한 곡 감상하기 | Explore Different Songs | いろんな曲を聴いてみる | 🟠 |
| `PR0073` | jukebox_tag_explorer | Tiered | 2 / 4 / 6 | g100 / g200 / g350 | 여러 장르 탐험하기 | Discover New Genres | いろんなジャンルを探検する | 🟠 |
| `PR0074` | jukebox_pause_first | OneTime | 1 | g30 | 잠시 멈춰보기 | Take a Musical Pause | ちょっと一休み | 🟡 |
| `PR0075` | jukebox_mode_switch | OneTime | 1 | g40 | 재생 모드 바꿔보기 | Switch Up the Playback Mode | 再生モードを変えてみる | 🟡 |
| `PR0076` | jukebox_shuffle_on | OneTime | 1 | g40 | 랜덤재생 켜보기 | Shuffle Things Up | ランダム再生をオンにする | 🟡 |
| `PR0077` | jukebox_seek_scrub | OneTime | 1 | g30 | 원하는 부분으로 이동해보기 | Skip to Your Favorite Part | 好きな場面まで移動する | 🟡 |
| `PR0078` | jukebox_volume_tune | OneTime | 1 | g30 | 음량 조절해보기 | Fine-Tune the Volume | 音量を調節してみる | 🟡 |
| `PR0079` | jukebox_ambience_open | OneTime | 1 | g30 | 환경음 팝업 열어보기 | Open the Ambience Panel | 環境音パネルを開く | 🟡 |
| `PR0080` | jukebox_ambience_first | OneTime | 1 | g40 | 환경음 하나 켜보기 | Turn On an Ambient Sound | 環境音を一つオンにする | 🟡 |
| `PR0081` | jukebox_ambience_variety | Tiered | 3 / 5 / 7 | g150 / g300 / g600, i2×1 | 여러 환경음 즐기기 | Collect Ambient Sounds | いろんな環境音を楽しむ | 🟠 |
| `PR0082` | jukebox_ambience_full_house | OneTime | 1 | g250, i2×1 | 일곱 소리 한 번에 켜기 | All Seven Sounds at Once | 7つの音を同時にオンにする | 🟠 |
| `PR0083` | jukebox_ambience_listen | Increment | 20N | g20 (매 레벨) | 환경음 감상 누적하기 | Soak in the Ambience | 環境音を積み重ねて聴く | 🟡 |
| `PR0084` | jukebox_ai_request | OneTime | 1 | g300, i3×1 | 아이코에게 음악 요청하기 | Ask Aiko to Play a Mood | アイコに音楽をリクエストする | 🔴 |
| `PR0085` | jukebox_deep_listen | Tiered | 10 / 30 / 60 | g150 / g350 / g600, i2×1 | 음악과 함께 몰입하기 | Get Lost in the Music | 音楽にじっくり浸る | 🔴 |
| `PR0086` | jukebox_dual_scape | OneTime | 1 | g200, i1×1 | 나만의 사운드 조합 만들기 | Craft Your Own Soundscape | 自分だけのサウンドを作る | 🟠 |
| `PR0087` | jukebox_custom_track | OneTime | 1 | g150, i1×1 | 나만의 음악 추가해보기 | Add Your Own Track | 自分の音楽を追加する | 🟠 |
| `PR0088` | perfect_week_attendance | Increment | 1N | g200 | 완벽한 한 주 | Perfect Week | パーフェクトウィーク | 🟠 |
| `PR0089` | daily_jukebox_streak | Tiered | 3 / 7 / 14 | g50 / g150 / g350 | 매일의 플레이리스트 | Daily Tunes Streak | 毎日のプレイリスト | 🟡 |
| `PR0090` | morning_ritual | OneTime | 1 | g100 | 굿모닝 루틴 | Good Morning Ritual | グッドモーニング・ルーティン | 🟡 |
| `PR0091` | focus_with_ambience | Tiered | 1 / 5 / 15 | g80 / g160 / g300, i2×1 | 소리와 함께 집중 | Focus in Soundscape | サウンドと集中 | 🟠 |
| `PR0092` | schedule_and_remind | OneTime | 1 | g100 | 일정에 알람 걸기 | Schedule with a Reminder | 予定にアラームを添えて | 🟡 |
| `PR0093` | alarm_voice_full | Tiered | 2 / 4 / 6 | g100 / g250 / g500 | 모든 목소리로 알람 맞추기 | Alarm in Every Voice | すべての声で目覚まし | 🟠 |
| `PR0094` | jukebox_all_modes | OneTime | 1 | g150 | 모든 재생 모드 경험 | Every Play Mode | 全再生モード体験 | 🟡 |
| `PR0095` | weekend_buddy | OneTime | 1 | g80 | 주말 단짝 | Weekend Buddy | 週末の相棒 | 🟢 |
| `PR0096` | weekday_perfect | Tiered | 3 / 4 / 5 | g100 / g200 / g400 | 평일 개근상 | Weekday Regular | 平日皆勤 | 🟠 |

_주요 훅: AlarmManager.AddDailyAlarm 성공 시 Report(id,1). alarms 리스트 길이로 파생하면 삭제 시 값이 줄어드니 반드시 생성 호출 지점에서 누적 카운트할 것. · AlarmManager.AddRelativeTimer 최초 성공 호출 시 Report(id,1). 진입점(AlarmUI 버튼/UIManager.ShowAlarmMini 자동생성)이 2곳이나 모두 이 메서드로 모이므로 한 곳만 훅. · AlarmManager.AlarmRang 이벤트에 새 리스너를 구독해 매 발행마다 Report(id,1). 이벤트가 이미 존재하고 AlarmUI.OnAlarmRang이 이미 구독 중이라 배선이 가장 쉬움. · AlarmManager.DeleteAlarm 호출 시 Report(id,1). 변경 감지 없이 항상 실행되는 단순 메서드. · AlarmManager.UpdateAlarmTitle에서 실제로 빈 제목 → 비어있지 않은 값으로 바뀌는 분기에서 ReportFlag(id). 메서드에 이미 early-return 변경 가드가 있어 중복 트리거 걱정 없음. · AlarmManager.ToggleDailyEnabled / ToggleEnabled 호출 시 Report(id,1). · AlarmManager.SetWeekdayEnabled 처리 후 해당 알람의 7개 요일 bool을 검사해 '전체 true(기본값=매일)'와 다른 조합이면 ReportFlag(id). · AlarmManager.SetExcludeWeekend(id, true)로 값이 실제 true로 바뀌는 순간 ReportFlag(id). · AlarmManager.UpdateAlarmSoundType에서 새 타입이 Character로 바뀔 때 ReportFlag(id). · AlarmManager.StartRelativeTimer 호출 시 Report(id,1). 상세패널/미니위젯 2종/리스트토글 총 4개 UI 경로가 모두 이 메서드로 모이는 단일 허브._

### CH — 도전 · 신규 19개 (기존 8개에 이어 CH0009부터)

| id | name | type | 목표 | 보상 | 한국어 | English | 日本語 | 연동 |
|----|------|------|------|------|--------|---------|--------|------|
| `CH0009` | item_one_collector | Tiered | 5 / 20 / 50 | g80 / g250 / g600 | 수집가의 시작 | Collector's Start | コレクターの一歩 | 🟡 |
| `CH0010` | item_two_collector | Tiered | 5 / 20 / 50 | g80 / g250 / g600 | 수집가의 성장 | Collector's Growth | コレクターの成長 | 🟡 |
| `CH0011` | item_three_collector | Tiered | 5 / 20 / 50 | g80 / g250 / g600 | 수집가의 완성 | Collector's Mastery | コレクターの極み | 🟡 |
| `CH0012` | full_set_starter | OneTime | 1 | g150, i1×1, i2×1, i3×1 | 삼색 조화 | Three of a Kind | 三色そろって | 🟡 |
| `CH0013` | reward_claim_marathon | Increment | 5N | g30 | 보상 마라톤 | Reward Marathon | 報酬マラソン | 🟠 |
| `CH0014` | challenge_tab_complete | OneTime | 1 | g500, i2×2 | 도전을 넘어선 도전 | Beyond the Challenge | 挑戦を超えて | 🟠 |
| `CH0015` | all_tabs_explorer | Tiered | 1 / 3 / 5 | g100 / g300 / g700 | 다재다능 | Jack of All Tabs | オールラウンダー | 🟠 |
| `CH0016` | completionist_journey | Tiered | 25 / 60 / 100 | g200 / g500 / g1000, i3×3 | 완주의 길 | The Completionist's Path | コンプリートへの道 | 🟠 |
| `CH0017` | time_with_aico | Tiered | 60 / 600 / 3000 | g100 / g400 / g1000 | 함께한 시간 | Time Together | 一緒に過ごした時間 | 🟠 |
| `CH0018` | everyday_companion | Tiered | 3 / 7 / 30 | g150 / g500 / g1500, i1×5 | 매일 함께 | Everyday Companion | 毎日一緒に | 🔴 |
| `CH0019` | daily_login_streak | Tiered | 3 / 7 / 30 | g100 / g300 / i2×1 | 하루도 빠짐없이 | Never a Day Apart | 毎日欠かさず | 🟠 |
| `CH0020` | comeback_after_absence | OneTime | 1 | g100 | 오랜만이야 | Welcome Back | おかえり | 🟢 |
| `CH0021` | daily_feature_variety | Tiered | 3 / 5 / 8 | g80 / g150 / g300 | 하루 다재다능 | Busy Day | 多才な一日 | 🟡 |
| `CH0022` | daypart_full_house | OneTime | 4 | g120 | 하루 네 번의 인사 | Dawn to Dusk | 一日四度のあいさつ | 🟡 |
| `CH0023` | weekend_companion | Increment | 2N | g60 | 주말에도 함께 | Weekend Buddy | 週末も一緒 | 🟢 |
| `CH0024` | streak_comeback_rebuild | OneTime | 1 | g150, i1×1 | 다시 쌓는 습관 | Rebuild the Streak | 習慣を取り戻す | 🟡 |
| `CH0025` | grand_morning_combo | OneTime | 1 | g500, i2×1, i3×1 | 완벽한 아침의 삼중주 | Perfect Morning Trifecta | 完璧な朝のトリオ | 🔴 |
| `CH0026` | holiday_together | Tiered | 1 / 3 / 5 | g150 / g300 / g600 | 특별한 날을 함께 | Holidays Together | 特別な日をいっしょに | 🟠 |
| `CH0027` | four_seasons | Tiered | 2 / 3 / 4 | g200 / g400, i2×1 / g800, i3×1 | 사계절을 함께 | Through the Seasons | 四季をともに | 🔴 |

_주요 훅: InventoryManager.GetItem(1) 값을 MissionList.UpdateDerived()에서 SetCurrent(신규 미션ID, inv.GetItem(1))로 연결 — getter는 이미 존재, 3줄 추가 수준. · InventoryManager.GetItem(2) 값을 MissionList.UpdateDerived()에서 SetCurrent(신규 미션ID, inv.GetItem(2))로 연결. · InventoryManager.GetItem(3) 값을 MissionList.UpdateDerived()에서 SetCurrent(신규 미션ID, inv.GetItem(3))로 연결. · UpdateDerived()에서 GetItem(1)>=1 && GetItem(2)>=1 && GetItem(3)>=1 조건을 SetCurrent(0 또는 1)로 연결 — 기존 getter 3개 조합, 신규 저장필드 불필요. · MissionList.ClaimReward() 내부에 claimedTotal 카운터를 신설해 호출마다 증가시키고, 5회 단위로 SetCurrent(claimedTotal)에 반영. · MissionList.AllTabDone(Challenge) 호출부에 '자기 자신(이 미션) 제외' 가드를 추가한 뒤 완료 시 SetCurrent(1). · 신규 함수: Onboarding/Conversation/Affection/Productivity/Challenge 5개 탭 중 claimedTiers>0인 미션이 하나라도 있는 탭의 개수를 세어 SetCurrent에 전달. AllTabDone과 유사 패턴 재사용 가능. · Increment 타입을 제외한 전체 미션을 순회해 만렙 클레임 완료 비율(%)을 계산한 뒤 SetCurrent로 반영하는 신규 로직. · GlobalTimeVariableManager.totalPlaySeconds/60 값을 MissionList.UpdateDerived()에서 SetCurrent(신규 미션ID, 분단위값)로 연결하는 Report 배선 1줄 추가. · 신규 시스템 필요: lastLoginDate/streakDays를 저장할 필드를 InventoryManager 또는 신규 매니저에 추가하고, 앱 시작 시 날짜 diff로 연속/끊김을 판정한 뒤 MissionList.SetCurrent로 연결._

### ST — 선톡(SmallTalk) · 신규 20개

| id | name | type | 목표 | 보상 | 한국어 | English | 日本語 | 연동 |
|----|------|------|------|------|--------|---------|--------|------|
| `ST0001` | st_first_smalltalk | OneTime | 1 | g50 | 첫 선톡 받기 | Icoh's First Hello | 最初の先トーク | 🟡 |
| `ST0002` | st_receive_many | Increment | 10N | g30 | 선톡 누적으로 받기 | Chatty Companion | おしゃべりの積み重ね | 🟡 |
| `ST0003` | st_enable_auto | OneTime | 1 | g30 | 자동 선톡 켜기 | Turn On Auto Chat | 自動先トークをON | 🟡 |
| `ST0004` | st_disable_auto | OneTime | 1 | g20 | 자동 선톡 꺼보기 | Turn Off Auto Chat | 自動先トークをOFF | 🟡 |
| `ST0005` | st_tune_interval | Increment | 5N | g20 | 선톡 간격 조절해보기 | Adjust the Rhythm | 間隔調整トライ | 🟡 |
| `ST0006` | st_fastest_interval | OneTime | 1 | g50 | 가장 빠른 선톡 간격 설정하기 | Speed Talker | 最速の間隔設定 | 🟡 |
| `ST0007` | st_interval_explorer | Tiered | 3 / 5 / 8 | g50 / g100 / g200 | 선톡 간격 다양하게 시도하기 | Interval Explorer | 間隔いろいろ試す | 🟠 |
| `ST0008` | st_idle_bond | Tiered | 5 / 20 / 50 | g50 / g150 / g300 | 심심할 때 말 걸어주는 아이코 | Icoh Breaks the Silence | 寂しい時に話しかけてくれるアイコ | 🟠 |
| `ST0009` | st_reply_related | Tiered | 3 / 10 / 30 | g50 / g150 / g300 | 선톡에 답해주기 | Answering Icoh Back | 先トークに答える | 🟡 |
| `ST0010` | st_ignore_once | OneTime | 1 | g20 | 선톡을 못 들은 척 해보기 | The One That Got Ignored | 先トークを聞き逃す | 🟡 |
| `ST0011` | st_impatient_knock | OneTime | 1 | g20 | 재촉하다 아이코에게 거절당하기 | Too Impatient | 焦って断られる | 🟡 |
| `ST0012` | st_hotkey_talk | Increment | 3N | g20 | 단축키로 먼저 말 걸기 | Hotkey Chat | ショートカットで話しかける | 🟡 |
| `ST0013` | st_menu_talk | OneTime | 1 | g20 | 메뉴에서 잡담 요청하기 | Menu Chat Request | メニューから雑談リクエスト | 🟡 |
| `ST0014` | st_operator_talk | OneTime | 1 | g20 | 오퍼레이터 모드에서 잡담 요청하기 | Operator Chat Request | オペレーターモードで雑談リクエスト | 🟡 |
| `ST0015` | st_all_paths | OneTime | 4 | g100 | 모든 방법으로 선톡 걸어보기 | Every Way to Say Hi | すべての方法で先トーク | 🟠 |
| `ST0016` | st_click_milestone | Tiered | 1000 / 5000 / 10000 | g100 / g300 / g500 | 클릭이 부른 선톡 | A Thousand Clicks, One Hello | クリックが呼ぶ先トーク | 🟡 |
| `ST0017` | st_aropla_greeting | OneTime | 1 | g30 | 아로플라에서 첫 인사 받기 | Aropla's First Greeting | アロプラの最初の挨拶 | 🟡 |
| `ST0018` | st_night_hello | OneTime | 1 | g100 | 깊은 밤 선톡 받기 | A Hello in the Dead of Night | 深夜の先トーク | 🔴 |
| `ST0019` | night_voice_reply | OneTime | 1 | g120 | 한밤의 목소리 답장 | Late-Night Voice Reply | 深夜の声の返事 | 🟠 |
| `ST0020` | special_day_greeting | OneTime | 1 | g300, i2×1 | 특별한 날의 인사 받기 | Receive a Special-Day Greeting | 特別な日の挨拶を受け取る | 🟠 |

_주요 훅: APIManager.CallSmallTalkStream() 완료 블록, pendingSmallTalkContent/isSmallTalkPending=true 설정 지점(약 414~419행)에 MissionList.Instance.ReportFlag(id) 1회 추가 · APIManager.CallSmallTalkStream() 완료 블록에서 매 발화 성공 시 MissionList.Instance.Report(id,1) 누적 · SettingManager.SetIsCharAutoSmallTalk(true) setter 내부에 ReportFlag(id) 추가 · SettingManager.SetIsCharAutoSmallTalk(false) setter 내부에 ReportFlag(id) 추가 · SettingManager.SetCharAutoSmallTalkInterval(float) 호출마다 Report(id,1) · GlobalTimeVariableManager.SyncSettingsFromManager()에서 smallTalkIntervalSeconds = Mathf.Max(5f, currentInterval) 클램프 도달 판정 시 ReportFlag(id) · SettingManager.SetCharAutoSmallTalkInterval(float) 호출부에 방문한 서로 다른 간격 값을 담는 HashSet<float>을 신설하고 ReportBest(id, distinctCount) · GlobalTimeVariableManager.TryTriggerSmallTalk() → APIManager.CallSmallTalkStream 호출 경로에 신설 오리진 플래그(auto)를 부여한 뒤 완료 시 ReportBest(id, count) · APIManager 스트리밍 분기 latestIntentSmallTalkAnswer=="on" && !string.IsNullOrEmpty(latestSmallTalkQuery) 판정 지점(약 1854~1865행)에서 ReportBest(id, count) · APIManager 약 2288행 "[SmallTalk] Trigger expired" 로그 지점(isSmallTalkPending 30초 무응답 리셋 분기)에 ReportFlag(id)_

### WB — 웹검색·지식 · 신규 16개

| id | name | type | 목표 | 보상 | 한국어 | English | 日本語 | 연동 |
|----|------|------|------|------|--------|---------|--------|------|
| `WB0001` | first_web_search | OneTime | 1 | g50 | 첫 실시간 탐색 | First Web Search | 初めてのウェブ検索 | 🟡 |
| `WB0002` | web_search_milestones | Tiered | 10 / 50 / 100 | g100 / g300 / g600 | 웹서치 마일스톤 | Web Search Milestones | ウェブ検索マイルストーン | 🟠 |
| `WB0003` | forced_web_search_first | OneTime | 1 | g30 | 직접 검색 요청 | Direct Search Request | 検索ボタンを押してみる | 🟡 |
| `WB0004` | forced_web_search_habit | Increment | 5N | g20 | 검색 버튼 애용가 | Search Button Regular | 検索ボタン愛用者 | 🟠 |
| `WB0005` | web_search_setting_on | OneTime | 1 | g30 | 웹검색 켜기 | Turn On Web Search | ウェブ検索をオンに | 🟡 |
| `WB0006` | web_search_setting_force | OneTime | 1 | g80 | 웹검색 강제 모드 | Force Web Search Mode | ウェブ検索を強制モードに | 🟡 |
| `WB0007` | web_search_success_tiered | Tiered | 5 / 20 / 50 | g80 / g200 / g400 | 성공적인 탐색 | Successful Lookups | 検索成功の積み重ね | 🟡 |
| `WB0008` | web_search_engine_diversity | Tiered | 2 / 3 / 5 | g100 / g250 / g500, i1×1 | 다양한 검색엔진 경험 | Search Engine Explorer | 検索エンジンの多様性 | 🔴 |
| `WB0009` | thinking_patience | Increment | 20N | g10 | 생각하는 동안의 기다림 | Patient Companion | 考え中を見守る | 🟡 |
| `WB0010` | long_wait_endurance | OneTime | 1 | g60 | 긴 기다림의 보답 | Worth The Wait | 長い待ち時間の先に | 🔴 |
| `WB0011` | router_web_search_first | OneTime | 1 | g50 | 라우터 실시간 탐색 첫걸음 | Router's First Search | ルーター検索デビュー | 🟡 |
| `WB0012` | router_web_search_habit | Increment | 10N | g20 | 라우터 탐색 단골 | Router Search Regular | ルーター検索の常連 | 🟠 |
| `WB0013` | multimodal_switch_first | OneTime | 1 | g40 | 멀티모달로 전환 | Switch To Multimodal | マルチモーダルへ切替 | 🟡 |
| `WB0014` | web_search_failure_recovery | OneTime | 1 | g50 | 실패 후 다시 묻기 | Try Again After A Miss | 失敗しても諦めない | 🟠 |
| `WB0015` | router_retry_persistence | Tiered | 1 / 3 / 5 | g60 / g150 / g300 | 끈기있는 관찰 요청 | Persistent Observer | 粘り強い観察リクエスト | 🟠 |
| `WB0016` | web_confirm_scenario_first | OneTime | 1 | g100 | 웹검색 의향 확인 응답 | Confirm The Search | 検索意向への回答 | 🔴 |

_주요 훅: APIManager의 스트림 파싱 루프 3곳(CallConversationStream 등) 내 `if (intent_info_is_intent_web == "on")` 분기(APIManager.cs L346-372, L1196-1210, L1796-1826) 안에 MissionManager.Report("web_search_used") 1줄 추가. · first_web_search와 동일 분기(is_intent_web=="on")에서 Report 호출, 단 누적치를 세는 영속 카운터가 필요. · ChatHandler.HandleInputWebSubmitButton() (ChatHandler.cs L112-144)에서 GameManager.Instance.isWebSearchForced = true 설정 직후 Report 추가. 실제 UI 버튼(WebSearchBtn)이 Root260607/260616.prefab에 바인딩되어 살아있음. · forced_web_search_first와 동일 지점, 누적 클릭수를 세는 신규 영속 카운터 필요. · SettingManager.SetAIWebSearch() (SettingManager.cs L419)에서 settings.ai_web_search_idx가 0(off)에서 1 이상(on/force)으로 바뀌는 최초 시점에 Report 추가. · SettingManager.SetAIWebSearch()에서 ai_web_search_idx가 최초로 2(force)에 도달하는 시점에 Report 추가. · DebugBalloonManager2.AddWebLog(keyword, method, content) (DebugBalloonManager2.cs L152) 호출부에서 method가 Fail(Keyword)/Fail(LLM)/Fail 계열이 아닐 때 Report 추가. · DebugBalloonManager2.AddWebLog의 method 인자(duckduckgo/Tavily/GoogleCSE/serper/brave)를 Set<string>으로 누적하는 신규 로직을 추가하고 집합 크기를 ReportBest로 전달. · NoticeManager.Notice("thinking") (NoticeManager.cs L27-38) 호출부(APIManager.cs L253-256, L578-582, L1037-1039, L1699-1702) 각각에 Report 추가. · 신규: NoticeManager.Notice("thinking") 최초 호출 시각을 기록하고, 이후 응답 완료 콜백까지의 델타가 임계값(예 15초) 이상이면 Report. APIManager 스트림 루프에 타이머 신설 필요._

### AG — AI 에이전트 조작 · 신규 18개

| id | name | type | 목표 | 보상 | 한국어 | English | 日本語 | 연동 |
|----|------|------|------|------|--------|---------|--------|------|
| `AG0001` | mouse_first_move | OneTime | 1 | g50 | 첫 클릭 대행 | First Assisted Click | はじめての代行クリック | 🟡 |
| `AG0002` | mouse_click_streak | Increment | 10N | g10 | 클릭 대행 누적 | Click Assistant Streak | クリック代行の積み重ね | 🟡 |
| `AG0003` | mouse_versatility | Tiered | 2 / 4 / 6 | g80 / g200 / g400 | 손끝의 달인 | Mouse Multitool | マウス万能術 | 🟠 |
| `AG0004` | proxy_smooth_operator | Tiered | 1 / 10 / 50 | g60 / g150 / g350 | 조용한 손길 | Silent Operator | 静かな操作者 | 🟡 |
| `AG0005` | keyboard_typist | Tiered | 1 / 50 / 200 | g50 / g180 / g450 | 대신 타이핑 | Typing On Your Behalf | 代わりにタイピング | 🟡 |
| `AG0006` | hotkey_combo_collector | Tiered | 1 / 6 / 10 | g60 / g220 / g500 | 단축키 수집가 | Hotkey Collector | ショートカット収集家 | 🟠 |
| `AG0007` | app_explorer | Tiered | 1 / 5 / 10 | g60 / g200 / g450 | 프로그램 탐험가 | App Explorer | アプリ探検家 | 🟠 |
| `AG0008` | clipboard_bridge | Tiered | 1 / 20 / 50 | g50 / g150 / g350 | 클립보드 배달부 | Clipboard Courier | クリップボード便 | 🟡 |
| `AG0009` | screenshot_scout | Tiered | 1 / 20 / 50 | g50 / g150 / g350 | 순간 포착 | Snapshot Scout | 瞬間キャッチ | 🟡 |
| `AG0010` | data_lifecycle | OneTime | 1 | g150, i1×1 | 완벽한 데이터 순환 | Full Data Cycle | データの一周 | 🟠 |
| `AG0011` | skill_archivist | Tiered | 1 / 5 / 10 | g70 / g200 / g450 | 스킬 서고지기 | Skill Archivist | スキルの記録者 | 🟡 |
| `AG0012` | sound_conductor | Increment | 20N | g15 | 효과음 지휘자 | Sound Conductor | 効果音の指揮者 | 🟡 |
| `AG0013` | mode_shifter | OneTime | 1 | g50 | 모드 전환 시작 | Mode Shift | モード切替デビュー | 🟡 |
| `AG0014` | operator_debut | OneTime | 1 | g100, i1×1 | 오퍼레이터 모드 첫 진입 | Operator Debut | オペレーターモード初体験 | 🟡 |
| `AG0015` | character_performer | Tiered | 1 / 10 / 30 | g50 / g150 / g350 | 대신 움직이기 | Stand-In Performer | 代わりに動く | 🟡 |
| `AG0016` | todo_delegate | Tiered | 1 / 10 / 30 | g50 / g150 / g350 | 할 일 대행 | Todo Delegate | タスク代行 | 🟡 |
| `AG0017` | agent_first_command | OneTime | 1 | g100, i1×1 | AI에게 화면을 맡기다 | Hand Over The Screen | 画面操作をおまかせ | 🟡 |
| `AG0018` | agent_success_streak | Increment | 5N | g40 | 대행 성공 기록 | Mission Accomplished, Automatically | 代行成功の積み重ね | 🟡 |

_주요 훅: ApiAgentFunctionMouseAction.PhysicalClick 실행 완료(ApiAgentFunctionManager.ExecuteAction("physical_click") onComplete) 지점에 Report 훅 · ApiAgentFunctionMouseAction.PhysicalClick 완료 지점, 영구 누적 카운터 · ApiAgentFunctionManager.ExecuteAction 분기(physical_click/drag/scroll, proxy_click/drag/scroll 6종) 호출 시 사용된 functionName Set에 Report · ApiAgentFunctionProxyMouseAction.ProxyClick/ProxyDrag/ProxyScroll의 true(성공) 반환 분기 · ApiAgentFunctionKeyboardAction.TypeText 실행 완료 지점 누적 카운터 · ApiAgentFunctionKeyboardAction.SendHotkey(modifier,key) 호출 시 (modifier,key) 조합 Set에 Report · ApiAgentFunctionSystemAction.RunProcess(fileName) 성공 실행 시 distinct fileName Set에 Report · ApiAgentFunctionSystemAction.ReadClipboardText / WriteClipboardText 완료 지점, 두 함수 합산 카운터 · ApiAgentFunctionScreenshotAction.CaptureAndSave / CaptureScreenCoroutine 완료 콜백 지점 · ApiAgentFunctionManager.ExecuteAction의 save_data→read_data→delete_data가 동일 path로 순차 호출되는 지점에 Report_

### AC — 캐릭터 액션 · 신규 15개

| id | name | type | 목표 | 보상 | 한국어 | English | 日本語 | 연동 |
|----|------|------|------|------|--------|---------|--------|------|
| `AC0001` | ac_first_dance_request | OneTime | 1 | g50 | 첫 댄스 요청 | First Dance Request | 初めてのダンスリクエスト | 🟡 |
| `AC0002` | ac_first_walk_left_request | OneTime | 1 | g50 | 첫 왼쪽 걸음마 | First Steps Left | 初めての左歩き | 🟡 |
| `AC0003` | ac_first_walk_right_request | OneTime | 1 | g50 | 첫 오른쪽 걸음마 | First Steps Right | 初めての右歩き | 🟡 |
| `AC0004` | ac_first_stop_request | OneTime | 1 | g50 | 멈춰! | Freeze! | ストップ! | 🟡 |
| `AC0005` | ac_dance_lover | Increment | 5N | g30 | 댄스 마니아 | Dance Fanatic | ダンス好き | 🟡 |
| `AC0006` | ac_daily_walk | Tiered | 3 / 10 / 25 | g100 / g250 / g500 | 산책 애호가 | Daily Stroller | お散歩好き | 🟡 |
| `AC0007` | ac_both_directions | OneTime | 1 | g80, i1×1 | 양방향 산책러 | Both Ways | 両方向お散歩 | 🟠 |
| `AC0008` | ac_all_rounder | OneTime | 1 | g150, i2×1 | 만능 재주꾼 | All-Rounder | なんでもできる子 | 🟠 |
| `AC0009` | ac_dance_variety | Tiered | 5 / 15 / 30 | g100 / g300 / g600 | 댄스 컬렉터 | Dance Collector | ダンスコレクター | 🔴 |
| `AC0010` | ac_walk_to_the_wall | OneTime | 1 | g100 | 벽까지 걸어보기 | Hit the Wall | 壁までお散歩 | 🔴 |
| `AC0011` | ac_manual_dance_control | OneTime | 1 | g50 | 내가 직접 시켜본 춤 | Hands-On Dance | 手動でダンス指示 | 🟡 |
| `AC0012` | ac_dance_shortcut | OneTime | 1 | g50 | 단축키 댄스 | Shortcut Shuffle | ショートカットダンス | 🟡 |
| `AC0013` | ac_freeze_frame_fun | OneTime | 1 | g80 | 스톱모션 놀이 | Freeze Frame Fun | ストップモーション遊び | 🟡 |
| `AC0014` | ac_vision_guided_dance | Increment | 10N | g40 | 눈으로 보고 춤춰줘 | Dance, But Make It Visual | 見て踊ってダンス | 🟡 |
| `AC0015` | cursor_chase | OneTime | 1 | g120, i1×1 | 커서를 따라오게 하기 | Make It Follow the Cursor | カーソルを追いかけさせる | 🟡 |

_주요 훅: ApiAgentFunctionManager.ExecuteAction()의 functionName=="character_dance" 분기(또는 ApiAgentFunctionAction.Dance() 진입부)에서 Report(id,1) · ApiAgentFunctionAction.WalkLeft() 진입 시 Report(id,1) · ApiAgentFunctionAction.WalkRight() 진입 시 Report(id,1) · ApiAgentFunctionAction.StopAction() 진입 시 Report(id,1) · ac_first_dance_request와 동일 지점(character_dance 분기)에서 Report(id, delta=1) 누적 · character_walk_left/character_walk_right 두 case 모두에서 동일 id로 Report(id,1) 누적(좌우 합산) · ApiAgentFunctionAction.WalkLeft()/WalkRight() 각각에서 개별 ReportFlag(예: walked_left/walked_right) 후 둘 다 true면 파생 완료 처리 · character_dance/character_walk_left/character_walk_right/character_stop 4개 case 각각에서 ReportFlag 후 4개 모두 true면 파생 완료 처리 · AnimationManager.Dance() 내부 randomIndex 산출부(L48)에 HashSet<int> 신규 저장을 추가하고 크기가 늘 때 ReportBest(id, set.Count) · PhysicsManager.MoveLeft()/MoveRight() 코루틴의 경계 체크 분기(L212-217, L237-242)에서 StopAllAnimations() 호출 시 Report(id,1)_

### SK — AI 스킬 · 신규 21개

| id | name | type | 목표 | 보상 | 한국어 | English | 日本語 | 연동 |
|----|------|------|------|------|--------|---------|--------|------|
| `SK0001` | first_skill_saved | OneTime | 1 | g80, i1×1 | 나만의 첫 스킬 | My First Skill | はじめてのスキル | 🟡 |
| `SK0002` | skill_save_count | Increment | 5N | g30 | 스킬 장인의 길 | Skill Crafter | スキル職人の道 | 🟠 |
| `SK0003` | skill_variety | Tiered | 3 / 6 / 10 | g100 / g250 / g450, i2×1 | 다재다능 스킬북 | Skill Collection | 多彩なスキル集 | 🟠 |
| `SK0004` | first_skill_used | OneTime | 1 | g80, i1×1 | 첫 스킬 사용 | First Skill Run | スキル初使用 | 🟡 |
| `SK0005` | skill_use_count | Increment | 10N | g25 | 손에 익은 스킬 | Skill Regular | 使い込むスキル | 🟠 |
| `SK0006` | skill_use_variety | Tiered | 3 / 6 / 10 | g100 / g250 / g450, i2×1 | 스킬 탐험가 | Skill Explorer | スキル探検家 | 🟠 |
| `SK0007` | skill_deleted | OneTime | 1 | g60 | 스킬 정리하기 | Skill Cleanup | スキル整理 | 🟡 |
| `SK0008` | skill_synced_server | OneTime | 1 | g70 | 서버와 연결되다 | Synced to the Cloud | サーバーと同期 | 🟡 |
| `SK0009` | catalog_browsing | Increment | 10N | g10 | 스킬 둘러보기 | Browsing Skills | スキル巡り | 🟢 |
| `SK0010` | catalog_language_switch | OneTime | 1 | g40 | 언어를 바꿔보다 | Switch It Up | 言語を変えてみる | 🟢 |
| `SK0011` | catalog_trilingual | Tiered | 1 / 2 / 3 | g50 / g120 / g220 | 3개 국어 마스터 | Trilingual Explorer | 三言語マスター | 🟠 |
| `SK0012` | data_first_save | OneTime | 1 | g60 | 첫 데이터 저장 | First Save | はじめてのデータ保存 | 🟡 |
| `SK0013` | data_first_read | OneTime | 1 | g60 | 첫 데이터 불러오기 | First Load | はじめてのデータ読込 | 🟡 |
| `SK0014` | sfx_first_play | OneTime | 1 | g50 | 첫 사운드 | First Sound | はじめての音 | 🟡 |
| `SK0015` | sfx_play_count | Increment | 10N | g20 | 사운드 애호가 | Sound Enthusiast | サウンド愛好家 | 🟠 |
| `SK0016` | sfx_variety | Tiered | 3 / 6 / 10 | g90 / g200 / g380, i2×1 | 사운드 컬렉터 | Sound Collector | サウンドコレクター | 🟠 |
| `SK0017` | sfx_format_both | OneTime | 1 | g80 | 두 가지 음원 형식 | Format Explorer | 音源フォーマット両制覇 | 🟠 |
| `SK0018` | sfx_alert_triggered | OneTime | 1 | g50 | 알림음이 울리다 | Alert Chime | アラート音が鳴る | 🟡 |
| `SK0019` | search_to_skill | OneTime | 1 | g150 | 검색해서 스킬로 남기기 | Search, Then Save as Skill | 検索してスキルに保存 | 🟠 |
| `SK0020` | custom_skill_rename | OneTime | 1 | g50 | 스킬에 나만의 이름 붙이기 | Name your own skill | スキルに自分だけの名前を | 🟢 |
| `SK0021` | skill_export_share | OneTime | 1 | g120, i1×1 | 내 스킬 공유하기 | Share your custom skill | 自作スキルを共有する | 🟡 |

_주요 훅: ApiAgentFunctionSkillManager.SaveSkill(key, frontmatter, body) 성공 분기(파일 쓰기 완료) — AI의 save_skill 호출과 SkillCatalogClient.OnSaveRequested(UI 저장) 양쪽 다 이 메서드로 모이므로 여기서 Report(1) · ApiAgentFunctionSkillManager.SaveSkill 성공 분기 — 누적 호출 수를 세는 카운터 신설 후 5회마다 Report · ApiAgentFunctionSkillManager.SaveSkill(skillKey) — 지금까지 저장된 서로 다른 skillKey의 Set을 누적 추적, Set.Count로 ReportBest 단계 판정 · ApiAgentFunctionSkillManager.ReadSkillBody(key) — File.Exists true인 성공 분기(본문 반환)에서 최초 1회 Report · ApiAgentFunctionSkillManager.ReadSkillBody 성공 호출 카운터 — 10회마다 Report · ApiAgentFunctionSkillManager.ReadSkillBody(skillKey) 성공 분기 — 서로 다른 skillKey Set 크기로 ReportBest · SkillView.OnDeleteClicked(2단계 확인 완료, deleteArmed=true) → DeleteRequested 이벤트 → SkillCatalogClient.OnDeleteRequested → ApiAgentFunctionSkillManager.DeleteSkill 성공 분기에서 Report(1) · SkillCatalogClient.PostCustomCoroutine — POST /skills/custom 성공(200) 시 로그를 남기는 지점에서 Report(1) · SkillView.OnSkillValueChanged → SkillSelected 이벤트 구독 카운터, 10회마다 Report · SkillView.OnLanguageValueChanged → LanguageChanged 이벤트 최초 1회 발생 시 Report(1)_

### MD — 대화 모드 · 신규 19개

| id | name | type | 목표 | 보상 | 한국어 | English | 日本語 | 연동 |
|----|------|------|------|------|--------|---------|--------|------|
| `MD0001` | md_aropla_first | OneTime | 1 | g80 | 아로플라 첫 대화 | First Aropla Chat | 初めてのアロプラ会話 | 🟡 |
| `MD0002` | md_aropla_use | Tiered | 3 / 10 / 25 | g150 / g400 / g900 | 아로나·프라나와 자주 수다 | Chat with Arona & Plana Often | アロナ・プラナとよく話す | 🟡 |
| `MD0003` | md_operator_first | OneTime | 1 | g80 | 오퍼레이터 모드 첫 진입 | Enter Operator Mode | オペレーターモード初挑戦 | 🟡 |
| `MD0004` | md_operator_use | Tiered | 3 / 10 / 25 | g150 / g400 / g900 | 오퍼레이터 모드 애용하기 | Operator Mode Regular | オペレーターモード常連 | 🟡 |
| `MD0005` | md_all_modes | OneTime | 3 | g200, i2×1 | 세 모드 모두 경험하기 | Try All Three Modes | 3つのモード全部体験 | 🟠 |
| `MD0006` | md_switch | Increment | 5N | g60 | 모드 자주 바꾸기 | Switch Modes Often | モードをよく切り替える | 🟡 |
| `MD0007` | md_aropla_chat | Increment | 10N | g50 | 3자 대화에서 말 걸기 | Talk in the Trio Chat | 3人トークで話しかける | 🟡 |
| `MD0008` | md_plana_first_reply | OneTime | 1 | g100, i1×1 | 프라나의 첫 대답 | Plana's First Reply | プラナの初めての返事 | 🟡 |
| `MD0009` | md_plana_click | OneTime | 1 | g90 | 프라나에게 직접 말 걸기 | Talk to Plana Directly | プラナに直接話しかける | 🟡 |
| `MD0010` | md_auto_chat | Increment | 2N+1 | g40 | 둘이서 이어가는 대화 지켜보기 | Watch Them Chat On Their Own | 二人だけの会話を見守る | 🟡 |
| `MD0011` | md_multimodal_switch | OneTime | 1 | g80, i1×1 | 멀티모달 모델로 전환하기 | Switch to a Multimodal Model | マルチモーダルモデルに切替 | 🟡 |
| `MD0012` | md_no_image_continue | OneTime | 1 | g60 | 이미지 없이 계속하기 | Continue Without an Image | 画像なしで続ける | 🟡 |
| `MD0013` | md_image_setting_open | OneTime | 1 | g80 | 이미지 영역 설정 열기 | Set Up Image Capture Area | 画像キャプチャ範囲を設定 | 🟡 |
| `MD0014` | md_image_setting_off | OneTime | 1 | g60 | 이미지 기능 꺼두기 | Turn Off Image Vision | 画像認識をオフにする | 🟡 |
| `MD0015` | md_first_image_needed | OneTime | 1 | g50 | 이미지가 필요하다는 안내 받기 | Get Your First 'Needs Image' Hint | 「画像が必要」を初めて聞く | 🟡 |
| `MD0016` | md_aropla_session_time | Tiered | 10 / 30 / 60 | g150 / g350 / g700 | 아로플라와 오래 머물기 | Linger in the Aropla Channel | アロプラでゆっくり過ごす | 🟠 |
| `MD0017` | md_operator_agent_task | Increment | 5N | g70 | 오퍼레이터에게 일 시키기 | Give Operator a Task | オペレーターに仕事を頼む | 🟠 |
| `MD0018` | md_aropla_streak | Tiered | 5 / 15 / 30 | g150 / g350 / g650 | 끊기지 않는 대화 이어가기 | Keep the Conversation Going | 会話を途切れさせない | 🟠 |
| `MD0019` | multimodal_and_web | OneTime | 1 | g150 | 보여주고 찾아보기 | Show It and Look It Up | 見せて調べて | 🟠 |

_주요 훅: APIAroPlaManager.StartAroplaChannel() 최초 호출 시 ReportFlag("md_aropla_first") · APIAroPlaManager.StartAroplaChannel() 호출마다 Report("md_aropla_use",1) · OperatorModeManager.EnterOperatorMode() 최초 호출 시 ReportFlag("md_operator_first") · OperatorModeManager.EnterOperatorMode() 호출마다 Report("md_operator_use",1) · ChatModeManager.SetMode(newMode)에 신설할 HashSet<ChatMode> 방문기록 — 새 모드가 집합에 추가될 때마다 Report("md_all_modes",1), 3종 모두 모이면 완료 · ChatModeManager.SetMode(newMode) 호출마다 Report("md_switch",1) · APIAroPlaManager.SendUserMessage(string) 호출(isAroplaMode 통과) 마다 Report("md_aropla_chat",1) · APIAroPlaManager.ProcessAroplaResponse(...)에서 speaker=="plana" 첫 분기 도달 시 ReportFlag("md_plana_first_reply") · ClickHandler.HandleLeftClick()/HandleClickMobile()에 ChatModeManager.IsAroplaMode() && clickedCharacter==APIAroPlaManager.Instance.GetPlanaInstance() 조건 한 줄 추가 후 ReportFlag("md_plana_click") · APIAroPlaManager.ProcessAroplaConversation 내부, next_speaker!="sensei"로 ContinueAroplaConversation(...) 호출 직전 Report("md_auto_chat",1)_

### VC — 음성 · 신규 20개

| id | name | type | 목표 | 보상 | 한국어 | English | 日本語 | 연동 |
|----|------|------|------|------|--------|---------|--------|------|
| `VC0001` | first_voice_message | OneTime | 1 | g50 | 첫 목소리 인사 | First Voice Hello | はじめての声のあいさつ | 🟠 |
| `VC0002` | voice_conversation_milestone | Tiered | 10 / 50 / 150 | g100 / g250 / g500 | 목소리로 대화하기 | Talk It Out Loud | 声で話してみよう | 🟡 |
| `VC0003` | voice_conversation_devotee | Increment | 20N | g30 | 목소리 단짝 | Voice Companion | 声のなかよし | 🟡 |
| `VC0004` | vad_first_use | OneTime | 1 | g40 | 자동 감지 첫 시도 | Hands-Free Debut | 自動音声検知はじめて | 🟡 |
| `VC0005` | vad_auto_send_milestone | Tiered | 5 / 20 / 50 | g80 / g200 / g400 | 자동으로 전해진 말 | Auto-Sent Words | 自動送信の言葉 | 🟡 |
| `VC0006` | max_recording_marathon | OneTime | 1 | g60 | 30초 끝까지 말하기 | Talk Till the Timer | 30秒フルで話す | 🟡 |
| `VC0007` | long_voice_message | OneTime | 1 | g70 | 긴 이야기 들려주기 | Long Story Told | 長いおしゃべり | 🟠 |
| `VC0008` | edit_before_send | OneTime | 1 | g50 | 말한 걸 다듬어 보내기 | Polish Before Sending | 話した言葉を直して送る | 🟠 |
| `VC0009` | stt_mode_explorer | OneTime | 1 | g60 | 인식 방식 바꿔보기 | Switch Recognition Modes | 認識方式を変えてみる | 🟠 |
| `VC0010` | voice_filter_explorer | OneTime | 1 | g40 | 목소리 필터 켜보기 | Try Voice Filter | 声フィルターを試す | 🟡 |
| `VC0011` | aico_voice_listener | Increment | 15N | g25 | 아이코 목소리 듣기 | Listen to AICO | アイコの声を聴く | 🟡 |
| `VC0012` | subcharacter_voice_debut | OneTime | 1 | g50 | 또 다른 목소리와 만남 | A New Voice Joins | もう一つの声との出会い | 🟡 |
| `VC0013` | multilingual_tts_listener | OneTime | 1 | g70 | 다른 언어로 듣기 | Hear Another Language | 別の言語で聴く | 🟠 |
| `VC0014` | daily_voice_habit | Tiered | 3 / 7 / 14 | g100 / g300 / g600 | 매일 목소리 습관 | Daily Voice Habit | 毎日の声の習慣 | 🟠 |
| `VC0015` | midnight_whisper | OneTime | 1 | g80 | 한밤의 속삭임 | Midnight Whisper | 真夜中のささやき | 🟠 |
| `VC0016` | dual_input_master | OneTime | 1 | g60 | 두 가지 말하기 방식 | Two Ways to Speak | 二つの話し方 | 🟠 |
| `VC0017` | voice_action_command | OneTime | 1 | g120 | 목소리로 움직여줘 | Move by My Voice | 声で動いてね | 🟠 |
| `VC0018` | jukebox_voice_request | OneTime | 1 | g80 | 음성으로 선곡 부탁 | Request a Song by Voice | 声で選曲リクエスト | 🟡 |
| `VC0019` | subchar_voice_collector | Tiered | 2 / 3 / 5 | g100 / g200 / g400 | 여러 목소리 모으기 | Voices of Many Friends | いろんな声を集めて | 🟡 |
| `VC0020` | voice_barge_in | OneTime | 1 | g100 | 말 끊고 다시 말하기 | Interrupt and Speak Again | 話を遮ってもう一度話す | 🟡 |

_주요 훅: WhisperSTTManager.ProcessSTTResponse / STTUtil.ProcessSTTResult — APIManager.CallConversationStream 호출 직전, '이전에 성공한 적 있는지'를 판단하는 최초 성공 플래그 신설 후 ReportFlag · WhisperSTTManager.ProcessSTTResponse / STTUtil.ProcessSTTResult 성공 분기(빈 텍스트/[BLANK_AUDIO] 아님) — CallConversationStream 호출 직전 MissionList.Report(id,1) · voice_conversation_milestone과 동일 지점(STT 성공 → CallConversationStream 직전)에 동일 이벤트로 함께 Report · VADController.StartVAD() (핫키 ActionStartTikitaka로 토글) — ReportFlag 1회 · VADController.saveBuffer() — UpdateVadStop()에서 2초 무음 경과 후 buffer.Length > 8000 조건 통과 시 MissionList.Report(id,1) · MicrophoneManager.Update() — Time.time - startRecordingTime >= maxRecordingDuration 분기(자동 StopRecording 호출부)에 ReportFlag · MicrophoneManager.StopRecording() — Time.time - startRecordingTime로 녹음 길이 계산 후 20초 이상이면 ReportFlag · WhisperSTTManager.ProcessSTTResponse / STTUtil.ProcessSTTResult의 editSttinChatInput=true 분기(ChatBalloonManager.AppendSTTTextToInputField 호출부) 이후, 실제 전송(제출) 시점에 ReportFlag · SettingManager.SetIsSTTServer(bool) 호출 시 true/false 두 값을 모두 경험했는지 추적하는 상태(2-flag) 신설 후 완료 시 ReportFlag · SettingManager.SetAiVoiceFilter() — settings.ai_voice_filter_idx 변경 시 ReportFlag_

### GM — 미니게임 · 신규 17개

| id | name | type | 목표 | 보상 | 한국어 | English | 日本語 | 연동 |
|----|------|------|------|------|--------|---------|--------|------|
| `GM0001` | start_20q_mode | OneTime | 1 | g50 | 첫 만남, 스무고개 | First Twenty Questions | はじめての20の質問 | 🟠 |
| `GM0002` | play_first_round | OneTime | 1 | g30 | 게임을 시작하다 | Game On | ゲーム開始 | 🟢 |
| `GM0003` | ask_questions | Increment | 10N | g20 | 질문 탐정 | Question Detective | 質問の探偵 | 🟢 |
| `GM0004` | win_secret_answer | OneTime | 1 | g100 | 첫 승리의 기쁨 | First Victory | はじめての勝利 | 🟢 |
| `GM0005` | collect_victories | Increment | 5N | g40 | 정답의 달인 | Answer Master | 正解のマスター | 🟢 |
| `GM0006` | reach_question_limit | OneTime | 1 | g20 | 한계까지 도전 | Pushed to the Limit | 限界までチャレンジ | 🟢 |
| `GM0007` | give_up_once | OneTime | 1 | g10 | 괜찮아, 다음에 또 | It's Okay, Try Again | 大丈夫、また挑戦しよう | 🟢 |
| `GM0008` | continue_after_miss | OneTime | 1 | g50 | 포기하지 않는 마음 | Never Give Up | あきらめない心 | 🟠 |
| `GM0009` | restart_game | OneTime | 1 | g30 | 다시 한번! | One More Round | もう一度！ | 🟠 |
| `GM0010` | discover_new_secrets | Tiered | 5 / 15 / 30 | g50 / g150 / g300 | 새로운 정답들 | New Discoveries | 新しい答えたち | 🟢 |
| `GM0011` | speedrun_win | Tiered | 10 / 8 / 5 | g100 / g200 / g400 | 스피드 추리왕 | Speed Sleuth | スピード推理王 | 🟡 |
| `GM0012` | win_streak | Tiered | 3 / 5 / 10 | g200 / g400 / g800 | 연승 기록 | Winning Streak | 連勝記録 | 🔴 |
| `GM0013` | total_games_played | Increment | 20N | g100 | 스무고개 애호가 | 20Q Enthusiast | 20の質問の愛好家 | 🔴 |
| `GM0014` | theme_explorer | Tiered | 3 / 6 / 10 | g150 / g300 / g500 | 테마 탐험가 | Theme Explorer | テーマ探検家 | 🔴 |
| `GM0015` | guess_attempts | Increment | 10N | g30 | 과감한 추측 | Bold Guesses | 果敢な推測 | 🟠 |
| `GM0016` | daily_minigame_streak | Tiered | 3 / 5 / 10 | g60 / g150 / g300 | 날마다 스무고개 | Daily 20 Questions | 毎日の二十の扉 | 🟠 |
| `GM0017` | theme_conqueror | Tiered | 2 / 4 / 6 | g100 / g250 / g500 | 모든 테마 정복 | Theme Conqueror | 全テーマ制覇 | 🟠 |

_주요 훅: MiniGame20QManager.Toggle20QMode() → StartNewGame() 최초 호출 시 Report · MiniGame20QManager.SetGameStatus(string status)의 status=="game_start" 분기 최초 호출 시 Report · MiniGame20QManager.SendQuestion(string query) 호출마다 Report · MiniGame20QManager.SetGameResult(string result)의 result=="user_won" 분기 최초 호출 시 Report · MiniGame20QManager.SetGameResult(string result)의 result=="user_won" 분기, 매 호출마다 Report · MiniGame20QManager.SetGameResult("max_reached") 최초 호출 시 Report · MiniGame20QManager.SetGameResult("user_gave_up") 최초 호출 시 Report · waitingFor=="continue_or_giveup" 상태 이후 다음 응답으로 전이되는 지점에 신규 로직을 추가해 Report · gameStatus의 game_over→game_start 재전이를 감지하는 신규 세션 카운터 지점에서 Report · MiniGame20QManager.SetHistorySecretList(List<string> newHistorySecretList) 호출 시 historySecretList.Count 값으로 ReportBest_

### VS — 비전/화면 · 신규 21개

| id | name | type | 목표 | 보상 | 한국어 | English | 日本語 | 연동 |
|----|------|------|------|------|--------|---------|--------|------|
| `VS0001` | first_area_select | OneTime | 1 | g50 | 화면에 첫 테두리 그리기 | Draw Your First Frame | はじめての範囲指定 | 🟡 |
| `VS0002` | area_screenshot_capture | Increment | 10N | g20 | 영역 스크린샷 모으기 | Snapshot Collector | エリア撮影コレクター | 🟡 |
| `VS0003` | show_clipboard_image | Tiered | 1 / 10 / 30 | g30 / g100 / g250 | 이미지로 대화 시작하기 | Show & Tell | 画像で話しかけよう | 🟡 |
| `VS0004` | ocr_run_count | Increment | 10N | g25 | 화면 텍스트 읽어내기 | Text Scanner | 画面テキスト解析 | 🟡 |
| `VS0005` | ocr_slot_diversity | OneTime | 3 | g150, i1×1 | OCR 옵션 마스터 | OCR Options Master | OCR設定マスター | 🟠 |
| `VS0006` | ocr_translate_use | Tiered | 1 / 10 / 30 | g30 / g100 / g250 | 번역으로 화면 이해하기 | Read Between Languages | 翻訳で画面を理解 | 🟡 |
| `VS0007` | ocr_tts_listen | Increment | 10N | g25 | 화면 텍스트 소리로 듣기 | Screen Reader | 画面読み上げ | 🟡 |
| `VS0008` | ocr_autoclick_success | Tiered | 1 / 10 / 30 | g50 / g150 / g300 | 자동클릭 성공시키기 | Auto-Click Ace | 自動クリック成功 | 🟡 |
| `VS0009` | ocr_rect_slot_setup | OneTime | 3 | g150, i1×1 | 나만의 OCR 영역 완성 | Custom Zones Complete | OCR範囲を完成させよう | 🟡 |
| `VS0010` | automap_slot_add | OneTime | 1 | g50 | 캐릭터 인식 시작하기 | Character Mapping Begins | キャラ認識をはじめよう | 🟡 |
| `VS0011` | automap_ocr_fill | Increment | 10N | g30 | OCR로 캐릭터 이름 채우기 | Name That Character | OCRでキャラ名を自動入力 | 🟡 |
| `VS0012` | automap_save | OneTime | 1 | g80 | 캐릭터 매핑 저장하기 | Save the Mapping | キャラマッピングを保存 | 🟡 |
| `VS0013` | vl_agent_click_success | Tiered | 1 / 10 / 30 | g50 / g150 / g300 | AI가 화면을 클릭하다 | AI Points and Clicks | AIが画面をクリック | 🟡 |
| `VS0014` | vl_planner_task_done | Tiered | 1 / 10 / 30 | g80 / g200 / g400 | AI에게 화면 작업 맡기기 | Delegate to AI | AIに画面作業をお願い | 🟡 |
| `VS0015` | vl_cancel_task | Increment | 10N | g10 | 마음이 바뀌면 취소하기 | Change of Heart | やっぱりキャンセル | 🟢 |
| `VS0016` | vl_engine_scenario_diversity | OneTime | 2 | g200, i2×1 | 시나리오 두 가지 자동진행 완료 | Two Stories, One AI | 2種類のシナリオ自動進行 | 🟡 |
| `VS0017` | vl_router_first_use | OneTime | 1 | g100 | 만능 AI 라우터 첫 사용 | Meet the Router | 万能AIルーター初体験 | 🟡 |
| `VS0018` | ai_action_diversity | OneTime | 3 | g150, i1×1 | AI에게 화면 보고 행동시키기 | Ask AI to Act | AIに動いてもらおう | 🟡 |
| `VS0019` | capture_ask_combo | OneTime | 1 | g80 | 찍어서 물어보기 | Snap and Ask | 撮って聞いてみる | 🟡 |
| `VS0020` | automap_all_characters | Tiered | 2 / 4 / 6 | g100 / g300 / g600 | 모든 캐릭터 매핑하기 | Map Every Character | 全キャラをマッピング | 🟠 |
| `VS0021` | file_drop_to_chat | OneTime | 1 | g100 | 파일을 끌어다 보여주기 | Drag a File to Share | ファイルをドラッグして見せる | 🟡 |

_주요 훅: ScreenshotManager.SelectArea() 코루틴 - 드래그 종료 후 isAreaSet=true 되는 지점 (ChatBalloonManager.SetLastImageSource("screenshot") 콜백 옆) · ScreenshotManager.SaveScreenshotCoroutine(showAfterSave) 성공 경로 · ChatBalloonManager.ShowClipboardImage() → ScreenshotManager.ShowClipboardImage() - imageBytes!=null 성공 경로 · ScreenshotOCRManager.CallOCRAPICoroutine() - APIManager.CallPaddleOCR 콜백 result!=null 성공 판정 직후 · ScreenshotOCRManager.ExecuteOCRWithSlot(options, slot) 진입부의 slot 값 · ScreenshotOCRManager.ProcessOCRResult() - options.useTranslate && options.displayResults 분기 · ScreenshotOCRManager.ExecuteTTS() 진입 / CallTTSWithText() 성공 호출 · ScreenshotOCRManager.ExecuteAutoClick() - ExecutorMouseAction.Instance.ClickAtPosition(...) 호출 직후 · ScreenshotOCRRectManager.SaveSelectedArea() 저장 완료 지점 - 슬롯1~3 모두 설정 시 · OCRAutoMapManager.AddMappingSlot()_

### WM — 창·표시 · 신규 13개

| id | name | type | 목표 | 보상 | 한국어 | English | 日本語 | 연동 |
|----|------|------|------|------|--------|---------|--------|------|
| `WM0001` | first_hideaway | OneTime | 1 | g50 | 잠깐 숨어볼게요 | Taking a Quick Hideaway | ちょっと隠れてみるね | 🟡 |
| `WM0002` | hideaway_habit | Tiered | 10 / 30 / 50 | g50 / g150 / g300 | 숨바꼭질은 저의 특기 | Hide-and-Seek Expert | かくれんぼは得意だよ | 🟡 |
| `WM0003` | collision_curious | OneTime | 1 | g30 | 창문이랑 부딪혀볼까? | Bumping Into Windows | ウィンドウにコツン | 🟡 |
| `WM0004` | gravity_curious | OneTime | 1 | g30 | 중력 스위치를 켜다 | Flipping the Gravity Switch | 重力スイッチオン | 🟡 |
| `WM0005` | freefall_habit | Increment | 10N | g15 | 자유낙하는 습관이니까 | Free Fall Habit | 自由落下がクセになる | 🟢 |
| `WM0006` | click_spark_count | Increment | 20N | g10 | 콕콕 스파크 | Tap for a Spark | ぽちぽちスパーク | 🟡 |
| `WM0007` | portrait_tap_count | Increment | 15N | g15 | 포트레이트 콕 찌르기 | Poke the Portrait | ポートレートをポチッと | 🟡 |
| `WM0008` | window_hopper | Tiered | 3 / 6 / 10 | g80 / g200 / g400 | 다른 창 위에 착지! | Landing on Other Windows | 他のウィンドウに着地！ | 🟠 |
| `WM0009` | click_through_toggle | OneTime | 1 | g50 | 클릭 통과 모드 켜보기 | Try Click-Through Mode | クリック透過モードを試す | 🟢 |
| `WM0010` | always_on_top_pin | OneTime | 1 | g50 | 항상 위에 고정하기 | Pin Always on Top | 常に手前に固定する | 🟢 |
| `WM0011` | multi_monitor_migrate | OneTime | 1 | g150, i1×1 | 다른 모니터로 이사가기 | Move to Another Monitor | 別のモニターへお引っ越し | 🟡 |
| `WM0012` | dizzy_from_shake | OneTime | 1 | g80 | 흔들어서 어지럽게 만들기 | Shake Until Dizzy | 揺さぶってクラクラさせる | 🟡 |
| `WM0013` | fullscreen_stealth | OneTime | 1 | g150 | 전체화면에선 조용히 | Quiet in Fullscreen | 全画面では静かに | 🟠 |

_주요 훅: TrayIconManager.MinimizeWindow(object,EventArgs) 최초 호출 시점 — 트레이 더블클릭(ToggleWindowState 경유) 또는 컨텍스트메뉴 'Hide' 클릭 시 공통 진입. HideWindow() 호출 직후 Report(1) 삽입. · TrayIconManager.MinimizeWindow(object,EventArgs) 매 호출마다 카운터 누적 → ReportTiered. first_hideaway와 동일 지점을 공유하므로 카운터 변수 하나로 두 미션 동시 판정 가능. · SettingManager.SetIsWindowsCollision(bool) 최초 호출 지점(설정 화면 isWindowsCollisionToggle 체크박스) → Report(1). · SettingManager.SetIsGravity(bool) 최초 호출 지점(설정 화면 isGravityToggle 체크박스) → Report(1). · FallingObject.StartFalling() (및 SubFallingObject 동일 메서드) — StatusManager.Instance.IsFalling=true 설정 직후 ReportIncrement 호출. · ClickEffecter.Update() 내 TransparentWindow.Instance.IsOnOpaquePixel 통과 후 SpawnFx() 호출 직후 ReportIncrement. · PortraitController.PlayAnimation(string)/PlayRandomAnimation() 호출 직후(Operator 모드) 및 PortraitClickHandler.HandleLeftClick() 호출 직후(일반 모드) 양쪽 경로에 ReportIncrement 삽입. · FallingObject.Update() 충돌 분기(WindowCollisionManager.GetTopOfCollisionRect(newPosition) 유효값 반환 지점)에서 창 핸들→제목을 식별해 고유 창 집합에 추가 → 집합 크기 변화 시 ReportTiered. · 클릭 통과(패스스루) 모드를 ON으로 토글하는 순간 Report · '항상 위' 고정 옵션을 켜는 순간 Report_

### HK — 핫키/입력 · 신규 19개

| id | name | type | 목표 | 보상 | 한국어 | English | 日本語 | 연동 |
|----|------|------|------|------|--------|---------|--------|------|
| `HK0001` | hotkey_chat_toggle | OneTime | 1 | g50 | 핫키로 대화창 열기 | Open Chat with a Hotkey | ホットキーでチャットを開く | 🟡 |
| `HK0002` | hotkey_char_chat_toggle | OneTime | 1 | g50 | 캐릭터에게 말 걸기 | Talk to Your Character | キャラクターに話しかける | 🟡 |
| `HK0003` | hotkey_radial_menu_open | OneTime | 1 | g50 | 액션 메뉴 열어보기 | Open the Action Menu | アクションメニューを開く | 🟡 |
| `HK0004` | hotkey_dance_party | Tiered | 1 / 10 / 50 | g50 / g150 / g400 | 댄스 파티 | Dance Party | ダンスパーティー | 🟡 |
| `HK0005` | hotkey_change_clothes | OneTime | 1 | g50 | 옷 갈아입기 | Change Outfits | 服を着替える | 🟡 |
| `HK0006` | costume_full_rotation | OneTime | 1 | g150, i1×1 | 코스튬 완전탐방 | Costume Collector | コスチューム全制覇 | 🟠 |
| `HK0007` | hotkey_new_chat | OneTime | 1 | g50 | 새로운 대화 시작 | Start a Fresh Conversation | 新しい会話を始める | 🟡 |
| `HK0008` | hotkey_voice_talk | Increment | 5N | g30/레벨 | 목소리로 대화하기 | Voice Chat Champion | 声で会話しよう | 🟡 |
| `HK0009` | hotkey_tikitaka_mode | OneTime | 1 | g50 | 티키타카 모드 켜기 | Turn On Tikitaka Mode | ティキタカモードをオンにする | 🟡 |
| `HK0010` | hotkey_screenshot_capture | Tiered | 1 / 10 / 30 | g50 / g150 / g300 | 스크린샷 마스터 | Screenshot Master | スクリーンショットマスター | 🟡 |
| `HK0011` | ocr_slot_explorer | OneTime | 1 | g150 | OCR 슬롯 올클리어 | OCR Slot Explorer | OCRスロット制覇 | 🟠 |
| `HK0012` | hotkey_momo_talk_skip | OneTime | 1 | g100 | 모모톡 스킵 성공 | Momo Talk Skip Success | モモトークスキップ成功 | 🟡 |
| `HK0013` | hotkey_bond_story_reader | OneTime | 1 | g150, i1×1 | 인연스토리 완독 | Finish the Bond Story | 縁ストーリーを読み終える | 🟡 |
| `HK0014` | hidden_f8_discovery | OneTime | 1 | g100, i1×1 | 숨겨진 개발자 모드 발견 | Discover the Secret Dev Mode | 隠された開発者モードを発見 | 🟠 |
| `HK0015` | hotkey_stop_meme | OneTime | 1 | g50 | 스탑 밈 체험 | Freeze-Frame Meme | ストップミームを体験 | 🟡 |
| `HK0016` | hotkey_custom_binding | OneTime | 1 | g50 | 나만의 핫키 설정 | Customize Your Hotkeys | 自分だけのホットキー設定 | 🟡 |
| `HK0017` | global_input_enable | OneTime | 1 | g50 | 백그라운드 핫키 활성화 | Enable Background Hotkeys | バックグラウンドホットキーを有効化 | 🟡 |
| `HK0018` | global_click_left_master | Increment | 1000N | g30/레벨 | 클릭왕의 길 | Path of the Click Master | クリック王の道 | 🟢 |
| `HK0019` | easter_egg_command | OneTime | 1 | g200, i2×1 | 숨겨진 커맨드 발견하기 | Discover the Secret Command | 隠しコマンドを見つける | 🟠 |

_주요 훅: HotKeyActionManager.actions["ActionChatStart"] → ChatBalloonManager.ToggleChatBalloonBottom() 호출부에 MissionList.Instance.ReportFlag("HK_ChatStart") 추가 · HotKeyActionManager.actions["ActionChatChar"] → ChatBalloonManager.ToggleChatBalloon() 호출부에 ReportFlag("HK_ChatChar") 추가 · HotKeyActionManager.actions["ActionCharAction"] → RadialMenu("RadialMenuAction").Show() 호출부에 ReportFlag("HK_RadialMenu") 추가 · HotKeyActionManager.actions["ActionDance"] → AnimationManager.Instance.Dance() 호출부에 Report("HK_Dance",1) 추가 · HotKeyActionManager.actions["ActionChangeClothes"] → CharManager.Instance.ChangeClothes() 호출부에 ReportFlag("HK_ChangeClothes") 추가 · CharManager.Instance.ChangeCostume() 내부에 신규 Set 트래커 추가, 보유 코스튬 전체 순회 완료 시 ReportFlag("HK_CostumeFullRotation") 호출 · HotKeyActionManager.actions["ActionNewChat"] → MemoryManager.Instance.ResetConversationMemoryAndGuide() 호출부에 ReportFlag("HK_NewChat") 추가 · HotKeyActionManager.actionsOnKeyUp["ActionStartTalk"] → MicrophoneManager.Instance.StopRecording() 호출부(키업 완료 지점)에 Report("HK_VoiceTalk",1) 추가 · HotKeyActionManager.actions["ActionStartTikitaka"] → VADController.Instance.ToggleVAD() 호출부에 ReportFlag("HK_Tikitaka") 추가 · HotKeyActionManager.actions["ActionExecuteAreaScreenshot"] → ScreenshotManager.SaveAndShowScreenshot() 호출부에 Report("HK_Screenshot",1) 추가_

### NT — 알림/메뉴 · 신규 20개

| id | name | type | 목표 | 보상 | 한국어 | English | 日本語 | 연동 |
|----|------|------|------|------|--------|---------|--------|------|
| `NT0001` | menu_open_master | Tiered | 1 / 20 / 100 | g20 / g100 / g300 | 메뉴의 달인 | Menu Master | メニューの達人 | 🟢 |
| `NT0002` | version_window_visit | OneTime | 1 | g30, i1×1 | 우리는 이런 사람들입니다 | Meet the Team | 私たちはこんな仲間です | 🟢 |
| `NT0003` | radial_menu_debut | Tiered | 1 / 10 / 30 | g20 / g80 / g200 | 라디얼 메뉴 첫걸음 | Radial Menu Debut | ラジアルメニュー初挑戦 | 🟢 |
| `NT0004` | right_click_discovery | OneTime | 1 | g30 | 우클릭의 발견 | Right-Click Discovery | 右クリックの発見 | 🟡 |
| `NT0005` | long_press_summon | OneTime | 1 | g30 | 꾹 눌러 부르기 | Press and Hold | 長押しで呼び出し | 🟡 |
| `NT0006` | double_click_menu | OneTime | 1 | g30 | 두 번 두드리기 | Double Tap | ダブルクリックで開く | 🟡 |
| `NT0007` | summon_friend_menu | OneTime | 1 | g40, i1×1 | 소환된 친구와의 대화 | Summoned Friend | 呼び出した仲間との対話 | 🟡 |
| `NT0008` | operator_mode_peek | OneTime | 1 | g50, i2×1 | 오퍼레이터의 방 | Operator's Room | オペレーターの部屋 | 🟡 |
| `NT0009` | menu_explorer | Tiered | 3 / 5 / 7 | g50 / g150 / g350 | 메뉴 구석구석 | Menu Explorer | メニューを隅々まで | 🟠 |
| `NT0010` | voice_panel_habit | Increment | 5N | g15 | 목소리로 말 걸기 | Talk to Me | 声をかけてみる | 🟡 |
| `NT0011` | notice_balloon_fan | Increment | 10N | g10 | 말풍선 여닫기 | Balloon Toggler | 吹き出し開閉 | 🟡 |
| `NT0012` | thinking_companion | Increment | 20N | g10 | 생각하는 AICO 지켜보기 | Watching AICO Think | 考えるAICOを見守る | 🟡 |
| `NT0013` | websearch_awakening | OneTime | 1 | g40, i1×1 | 검색하는 AICO를 처음 보다 | AICO Goes Online | 検索するAICOを初めて見る | 🟡 |
| `NT0014` | radial_action_regular | Tiered | 1 / 15 / 50 | g20 / g100 / g250 | 라디얼 액션 단골 | Radial Regular | ラジアルアクションの常連 | 🟡 |
| `NT0015` | radial_action_collector | OneTime | 5 | g80, i2×1 | 다섯 가지 몸짓 | Five Moves | 5つの仕草を全部 | 🟠 |
| `NT0016` | new_look | OneTime | 1 | g40, i1×1 | 옷장을 열다 | New Look | 新しい装い | 🟡 |
| `NT0017` | secret_developer_tab | OneTime | 1 | g60, i2×1 | 숨겨진 단추 | Hidden Switch | 隠されたスイッチ | 🟡 |
| `NT0018` | graceful_farewell | Increment | 5N | g25 | 다음에 또 만나요 | See You Soon | またね、また会おう | 🟡 |
| `NT0019` | radial_all_entries | OneTime | 1 | g150 | 메뉴 여는 모든 방법 | Every Way In | メニューを開く全方法 | 🟡 |
| `NT0020` | middle_click_secret | OneTime | 1 | g80 | 가운데 버튼의 비밀 | The Middle-Click Secret | 中クリックの秘密 | 🟡 |

_주요 훅: DevionGames ContextMenu 위젯(m_ContextMenu, MenuTrigger/SubMenuTrigger/OperatorMenuTrigger가 WidgetUtility.Find로 공유 참조).RegisterListener("OnShow", cb) — UIWidget.Show() 호출 시 자동 발생하는 OnShow에서 Report · version 오브젝트의 UIWidget.RegisterListener("OnShow", cb) — UIManager.ShowVersion() 호출 경로에서 자동 발생하는 OnShow에서 Report · m_RadialMenuAction(RadialMenu 위젯).RegisterListener("OnShow", cb) — RadialMenu.Show()가 base.Show()를 호출해 발생하는 OnShow에서 Report · MenuTrigger.OnPointerDown(PointerEventData) 내 Right 버튼 분기, TriggerMenu() 호출 직전에 Report 신규 삽입 · MenuTrigger.Update() 내 isLeftClickHeld && leftClickHoldTime>=0.5f 분기, TriggerMenu() 호출 직전에 Report (동일 로직이 SubMenuTrigger/OperatorMenuTrigger에도 복제돼 있어 각각 동일 훅 필요) · MenuTrigger.OnDoubleClick() / OperatorMenuTrigger.OnDoubleClick() 진입 시 Report · SubMenuTrigger.TriggerMenu() 진입 시 Report · OperatorMenuTrigger.TriggerMenu() (ChatModeManager.Instance.IsOperatorMode() 참인 경로)에서 Report · MenuTrigger.TriggerMenu() 내 Settings/Character/Chat/Control/Function/Version/Exit 등 각 AddMenuItem/AddSubMenuItem delegate(약 20개 지점)에 항목 식별자 Report 삽입 · TalkMenuManager.ShowTalkMenu() 호출 시 Report_

### SE — 설정·언어·모델 · 신규 21개

| id | name | type | 목표 | 보상 | 한국어 | English | 日本語 | 연동 |
|----|------|------|------|------|--------|---------|--------|------|
| `SE0001` | first_settings_open | OneTime | 1 | g30 | 환경 설정, 첫 방문 | First Steps Into Settings | 設定への第一歩 | 🟡 |
| `SE0002` | settings_habit_tracker | Increment | 15N | g20 | 설정 다듬기 달인 | Settings Tinkerer | 設定のこだわり屋 | 🟠 |
| `SE0003` | settings_tab_explorer | Tiered | 3 / 5 / 8 | g30 / g60 / g100 | 설정 탐험가 | Settings Explorer | 設定探検家 | 🟠 |
| `SE0004` | ui_language_switch | OneTime | 1 | g20 | 새 언어로 인사하기 | Say Hello in a New Language | 新しい言語でこんにちは | 🟡 |
| `SE0005` | language_trilingual | OneTime | 3 | g80 | 3개 국어 마스터 | Trilingual Explorer | 三か国語マスター | 🟠 |
| `SE0006` | ai_language_customized | OneTime | 1 | g20 | AI와의 대화 언어 정하기 | Tune AICO's Voice | AIとの言葉を選ぶ | 🟡 |
| `SE0007` | server_type_explorer | Tiered | 2 / 3 / 4 | g40 / g80 / g150 | 서버 유형 편력가 | Server Type Voyager | サーバータイプの旅人 | 🟠 |
| `SE0008` | dual_engine_operator | OneTime | 2 | g60 | CPU도 GPU도 다뤄보기 | CPU & GPU, Both Mastered | CPUもGPUも使いこなす | 🟠 |
| `SE0009` | first_model_download | OneTime | 1 | g50 | 첫 모델 내려받기 | First Model Downloaded | 初めてのモデルダウンロード | 🟡 |
| `SE0010` | model_collector | Tiered | 2 / 4 / 6 | g60 / g120 / g200 | 모델 컬렉터 | Model Collector | モデルコレクター | 🟠 |
| `SE0011` | local_model_first_boot | OneTime | 1 | g70 | 로컬 서버, 첫 시동 | Local Server, First Boot | ローカルサーバー初起動 | 🟠 |
| `SE0012` | gemini_key_verified | OneTime | 1 | g40 | 제미니 키 인증 성공 | Gemini Key Verified | Geminiキー認証成功 | 🟡 |
| `SE0013` | custom_model_manual_entry | OneTime | 1 | g30 | 나만의 모델 이름 짓기 | Name Your Own Model | 自分だけのモデル名を入力 | 🟡 |
| `SE0014` | web_search_unlocked | OneTime | 1 | g40 | 웹서치 기능 켜기 | Web Search Unlocked | ウェブ検索を解禁 | 🟡 |
| `SE0015` | multimodal_vision_enabled | OneTime | 1 | g30 | 이미지도 보여주기 | Show AICO a Picture | AICOに画像を見せる | 🟡 |
| `SE0016` | emotion_expression_unlocked | OneTime | 1 | g40 | 감성표현 기능 켜기 | Feelings, Unlocked | 感情表現をオンにする | 🟡 |
| `SE0017` | hotkey_master | OneTime | 1 | g30 | 어디서든 한 번에 호출 | Summon AICO Anywhere | どこからでも呼び出せる | 🟡 |
| `SE0018` | edition_ascension | Tiered | 1 / 2 | g100 / g300, i2×1 | 에디션 승급 | Edition Ascension | エディションアップグレード | 🟡 |
| `SE0019` | model_talk_diversity | Tiered | 2 / 4 / 6 | g100 / g250 / g500 | 여러 두뇌와 대화하기 | Talk Across Models | いろんなモデルと対話 | 🟠 |
| `SE0020` | all_features_enabled | OneTime | 1 | g150 | 모든 기능 켜기 | All Features On | 全機能オン | 🟡 |
| `SE0021` | settings_export_backup | OneTime | 1 | g100 | 내 설정 백업하기 | Back up your settings | 設定をバックアップする | 🟡 |

_주요 훅: UIManager.showSettings() 최초 호출 시 Report 한 줄 추가 · SettingManager.SaveSettings() 호출마다 전역 카운터 증가(신설) → 15의 배수 도달 시 Report · SettingsMenuController.OnMenuButtonClicked(int selectedIndex)에서 방문한 탭 index를 HashSet에 누적, size 도달 시 ReportBest/Report · SettingManager.SetUiLanguage() 최초 호출 시 Report · SettingManager.SetUiLanguage()에서 선택된 언어 idx를 HashSet에 누적, size==3 도달 시 Report · SettingManager.SetAiLanguage() 최초 호출 시 Report · SettingManager.SetServerType()/SetServerTypeByValue(int)에서 선택된 타입 값을 HashSet에 누적, size 도달 시 Report · SettingManager.SetServerLocalMode()에서 선택된 모드를 HashSet에 누적, size==2 도달 시 Report · DownloadManager.SequentialDownloadCoroutine() 완료 지점(isDownloading=false 직전)에서 Report · SettingManager.SetServerModelType()에서 선택된 모델명을 HashSet에 누적, size 도달 시 Report_

### ME — 기억 · 신규 25개

| id | name | type | 목표 | 보상 | 한국어 | English | 日本語 | 연동 |
|----|------|------|------|------|--------|---------|--------|------|
| `ME0001` | retry_answer_regen | Increment | 5N | g20 | 답변 다시 써보기 | Try a Different Reply | 返信をやり直す | 🟢 |
| `ME0002` | chat_history_open | Tiered | 1 / 5 / 20 | g30 / g80 / g150 | 지난 대화 들춰보기 | Look Back at Old Chats | 昔の会話を見返す | 🟡 |
| `ME0003` | reset_memory_once | OneTime | 1 | g50 | 기억을 새로 시작해보기 | Start Fresh Memories | 記憶をリセットしてみる | 🟡 |
| `ME0004` | undo_last_message | OneTime | 1 | g30 | 방금 대화 되돌리기 | Undo the Last Message | 直前の会話を取り消す | 🟡 |
| `ME0005` | system_whisper_first | OneTime | 1 | g30 | 아이코가 먼저 건 말 들어보기 | Hear Aico Reach Out First | アイコから先に話しかけられる | 🟡 |
| `ME0006` | edit_persona_card | OneTime | 1 | g40 | 유저카드 다시 써보기 | Rewrite a Persona Card | ユーザーカードを書き直す | 🟡 |
| `ME0007` | erase_persona_card | OneTime | 1 | g30 | 유저카드 지워보기 | Erase a Persona Card | ユーザーカードを消してみる | 🟡 |
| `ME0008` | toggle_persona_card | OneTime | 1 | g20 | 유저카드 켜고 꺼보기 | Flip a Persona Card On/Off | ユーザーカードのON/OFFを試す | 🟡 |
| `ME0009` | keep_active_persona_cards | Tiered | 2 / 3 / 5 | g50 / g100 / g200 | 여러 얼굴 동시에 켜두기 | Keep Multiple Personas Active | 複数の人格を同時にオンにする | 🟠 |
| `ME0010` | create_persona_card | OneTime | 1 | g40 | 나만의 유저카드 만들기 | Create Your Own Persona Card | 自分だけのユーザーカードを作る | 🟠 |
| `ME0011` | pet_sub_character | OneTime | 1 | g30 | 곁에 있는 아이 쓰다듬기 | Pet a Companion Character | 傍にいる子をなでてみる | 🟡 |
| `ME0012` | click_sub_character_reaction | Increment | 10N | g15 | 곁에 있는 아이 반응 구경하기 | Watch a Companion React | 傍にいる子の反応を見る | 🟡 |
| `ME0013` | hear_all_voice_types | Tiered | 1 / 2 / 4 | g30 / g80 / g150 | 아이코의 목소리 4가지 모두 듣기 | Hear All of Aico's Voice Lines | アイコの声を4種類とも聞く | 🟠 |
| `ME0014` | meet_many_characters | Tiered | 2 / 4 / 6 | g50 / g120 / g250 | 여러 친구와 대화 쌓아가기 | Build Memories with Many Friends | いろんな子と会話を積み重ねる | 🟠 |
| `ME0015` | operator_mode_first_chat | OneTime | 1 | g40 | 오퍼레이터 모드로 처음 대화하기 | First Chat in Operator Mode | オペレーターモードで初めて話す | 🟡 |
| `ME0016` | voice_chat_first | OneTime | 1 | g30 | 목소리로 처음 말 걸어보기 | First Voice Message | 初めて声で話しかける | 🟡 |
| `ME0017` | memory_keeper_milestone | Tiered | 50 / 200 / 500 | g100 / g300 / g600 | 함께 쌓아온 추억들 | Memories We've Built Together | 一緒に積み上げた思い出 | 🟡 |
| `ME0018` | persona_card_collector | Tiered | 3 / 5 / 10 | g100 / g250 / g500 | 유저카드 도서관 | Persona Card Library | ユーザーカード図書館 | 🟠 |
| `ME0019` | first_week_anniversary | OneTime | 1 | g100 | 함께한 지 일주일 | One Week Together | いっしょに一週間 | 🟡 |
| `ME0020` | hundred_days_together | OneTime | 1 | g500, i2×1 | 우리의 백일 | Our Hundredth Day | ふたりの百日 | 🔴 |
| `ME0021` | register_birthday | OneTime | 1 | g50 | 생일 등록하기 | Register Your Birthday | 誕生日を登録 | 🟢 |
| `ME0022` | birthday_surprise | OneTime | 1 | g300, i3×1 | 생일 축하받기 | Birthday Surprise | 誕生日のサプライズ | 🟠 |
| `ME0023` | comeback_reunion | OneTime | 1 | g100 | 오랜만의 재회 | Long-Awaited Reunion | ひさしぶりの再会 | 🟡 |
| `ME0024` | persona_card_export_share | OneTime | 1 | g120 | 유저카드 내보내 공유하기 | Export & share your user card | ユーザーカードを書き出して共有 | 🟡 |
| `ME0025` | persona_card_swap_context | Increment | 5N | g40 | 상황에 맞게 유저카드 바꾸기 | Swap user cards by mood | 気分でユーザーカードを切り替える | 🟢 |

_주요 훅: AnswerBalloonManager.ChatRegenerate() 호출 시점마다 Report (카운터는 전송 시 리셋되지만 호출 자체를 매번 집계) · UIManager.ShowChatHistory()/ToggleChatHistory() 호출 시 ReportFlag(최초)+Report(누적) · MemoryManager.ResetConversationMemoryAndGuide() 내부 1곳에 ReportFlag (HotKeyActionManager/MenuTrigger/OperatorMenuTrigger 3개 호출부가 공통 경유) · MemoryManager.DeleteRecentDialogue() — AnswerBalloonManager.DeleteRecentDialogue()/APIAroPlaManager.cs:1503 공통 진입점에 ReportFlag · MemoryManager.SaveSystemMemory 호출부(ScenarioAskManager.cs:65,145 / ScenarioUtil.cs:12)에 ReportFlag · UIUserCardSlotController.FinalizeInputEdit() → UIUserCardManager.UpdateUserCard() (fixButton)에 ReportFlag · UIUserCardSlotController.OnClickEraseButton() → UIUserCardManager.RemoveUserCard() (eraseButton)에 ReportFlag · UIUserCardSlotController.ToggleActiveState() → UIUserCardManager.SetCardActive() (textFrameButton)에 ReportFlag · UIUserCardManager에 ActiveCardCount getter 신설 → MissionList.UpdateDerived()에서 SetCurrent · UIUserCardManager.AddUserCard()/AddUserCardSlot() 또는 SaveUserCardInfosToJson(saveButton) 확정 시 ReportFlag_

### MR — MR/XR · 신규 20개

| id | name | type | 목표 | 보상 | 한국어 | English | 日本語 | 연동 |
|----|------|------|------|------|--------|---------|--------|------|
| `MR0001` | mr_character_menu_open | OneTime | 1 | g50 | 첫 만남, 커스터마이징 메뉴 | First Look: Customization Menu | はじめての着せ替えメニュー | 🟡 |
| `MR0002` | mr_skin_collector | Tiered | 2 / 4 / 6 | g100 / g250 / g500, i1×1 | 스킨 컬렉터 | Skin Collector | スキンコレクター | 🟠 |
| `MR0003` | mr_character_tuner | OneTime | 1 | g80 | 나만의 캐릭터 튜닝 | Tune Your Companion | マイキャラクター調整 | 🟠 |
| `MR0004` | mr_ball_play | Increment | 10N | g20 | 볼 놀이 애호가 | Ball Enthusiast | ボール遊び好き | 🟡 |
| `MR0005` | mr_gentle_pat | Increment | 10N | g20 | 다정한 손길 | Gentle Hands | やさしい手つき | 🟡 |
| `MR0006` | mr_tickle_master | OneTime | 1 | g100 | 웃음 폭발의 순간 | Tickle Overload | 大笑いの瞬間 | 🟡 |
| `MR0007` | mr_smash_combo | Tiered | 10 / 30 / 50 | g100 / g300 / g500 | 꿀밤 콤보왕 | Smash Combo King | コツンコンボ王 | 🟡 |
| `MR0008` | mr_triple_touch | OneTime | 1 | g120 | 세 가지 손길 마스터 | Triple Touch | 三種の触れ合い | 🟠 |
| `MR0009` | mr_tap_trick | OneTime | 1 | g80 | 톡 건드리기 스킬 | The Perfect Tap | ちょんと触れるコツ | 🟠 |
| `MR0010` | mr_gesture_explorer | Tiered | 2 / 3 / 4 | g60 / g150 / g300 | 손짓 탐험가 | Gesture Explorer | ジェスチャー探検家 | 🟠 |
| `MR0011` | mr_first_beat | OneTime | 1 | g50 | 첫 곡, 재생 | First Beat | はじめての再生 | 🟡 |
| `MR0012` | mr_playlist_explorer | Tiered | 3 / 6 / 10 | g100 / g250 / g450 | 플레이리스트 탐험 | Playlist Explorer | プレイリスト探検 | 🟠 |
| `MR0013` | mr_full_listen | Increment | 5N | g30 | 끝까지 함께 듣기 | Listen to the End | 最後まで聴く | 🟡 |
| `MR0014` | mr_ambient_dj | OneTime | 1 | g60 | 공간을 채우는 소리 | Ambient DJ | 空間を満たす音 | 🟡 |
| `MR0015` | mr_anchor_architect | Tiered | 1 / 5 / 10 | g80 / g200 / g400 | 공간의 설계자 | Space Architect | 空間のアーキテクト | 🟡 |
| `MR0016` | mr_room_scan | OneTime | 1 | g100 | 내 방 스캔 완료 | Room Scan Complete | お部屋スキャン完了 | 🟡 |
| `MR0017` | mr_keyboard_typist | OneTime | 1 | g40 | 가상 키보드로 첫 메시지 | First Words Typed | 仮想キーボード初入力 | 🟢 |
| `MR0018` | mr_object_keeper | OneTime | 1 | g60 | 물건이 자리를 찾다 | A Place for Everything | 物の定位置 | 🟡 |
| `MR0019` | mr_hand_menu_open | OneTime | 1 | g150, i1×1 | 손바닥 위 메뉴 열기 | Open the Palm Menu | 手のひらメニューを開く | 🟠 |
| `MR0020` | mr_showcase_capture | OneTime | 1 | g120 | 내 방 속 아이코 찍기 | Capture AICO in your room | 部屋の中のアイコを撮影 | 🟡 |

_주요 훅: MRCharacterMenu.OpenMenu() 최초 호출 지점에 Report · MRSpineCharacterController.ApplySkinByIndex(index,false) 호출부에서 착용해본 스킨 id 집합 크기를 ReportBest · MRCharacterMenu.OnSliderChanged(prefKey,...) — Idle 딜레이/확률/간격 3종 슬라이더를 모두 조정했을 때 Report · MRSpineCharacterController.BeginInteraction(worldPoint, "Ball") 호출마다 Report · MRSpineCharacterController.EnterState(State.Pat) 진입 시 Report · MRSpineCharacterController.EnterState() 의 State.Tickle2 분기 진입 시 Report · MRSpineCharacterController.EnterState() Smash 케이스 내 _smashHitCount 갱신 지점에서 ReportBest · MRSpineCharacterController.BeginInteraction(worldPoint, colliderTag) — Ball/Pat/Tickle 태그 집합이 3종 모두 채워지면 Report · MRHandInteractionRouter.ProcessHand() 의 tickleTapArmed 판정 성공 지점(225~239행) · MRHandInteractionRouter.ProcessHand() 의 currentGesture 계산부(146행) — 인식해본 제스처 종류 집합 크기를 ReportBest_

### XX — 기타(미분류) · 신규 17개

| id | name | type | 목표 | 보상 | 한국어 | English | 日本語 | 연동 |
|----|------|------|------|------|--------|---------|--------|------|
| `XX0001` | hotkey_customize | OneTime | 1 | g100 | 나만의 단축키 | My Own Shortcut | 私だけのショートカット | 🟠 |
| `XX0002` | hotkey_action_variety | Tiered | 3 / 8 / 15 | g50 / g150 / g300 | 단축키 마스터 | Shortcut Master | ショートカット達人 | 🟠 |
| `XX0003` | clipboard_ai_share | OneTime | 1 | g80 | 클립보드로 대화하기 | Chat via Clipboard | クリップボードで会話 | 🟡 |
| `XX0004` | screenshot_capture | Tiered | 1 / 10 / 30 | g50 / g150 / g300 | 화면 캡처하기 | Screen Capture | 画面キャプチャ | 🟡 |
| `XX0005` | ocr_combo_variety | Tiered | 1 / 3 / 6 | g80 / g200 / g350 | OCR 조합 탐험가 | OCR Combo Explorer | OCRコンボ探検家 | 🟠 |
| `XX0006` | edition_upgrade | Tiered | 1 / 2 / 3 | g100 / g250 / g400, i2×1 | 에디션 업그레이드 | Edition Upgrade | エディションアップグレード | 🟡 |
| `XX0007` | server_first_connect | OneTime | 1 | g100 | AI와 첫 연결 | First Connection | AIとの初めての接続 | 🟢 |
| `XX0008` | setup_tutorial_complete | OneTime | 1 | g150 | 설정 마법사 완주 | Setup Wizard Complete | 設定ウィザード完走 | 🟡 |
| `XX0009` | compute_path_explorer | OneTime | 1 | g80 | 나만의 연산 방식 | Choose Your Engine | 自分だけの計算方式 | 🟠 |
| `XX0010` | dlc_download_complete | Tiered | 1 / 3 / 6 | g100 / g250 / g400, i1×1 | 새로운 모습 받기 | New Look Unlocked | 新しい姿を手に入れる | 🟡 |
| `XX0011` | local_model_download | OneTime | 1 | g150, i2×1 | 나만의 AI 완성 | My Own AI, Complete | 自分だけのAI完成 | 🟡 |
| `XX0012` | agent_delegate_task | Increment | 5N | g60 | AI에게 일 맡기기 | Delegate to AI | AIに仕事を任せる | 🟠 |
| `XX0013` | custom_skill_create | Tiered | 1 / 3 / 5 | g100 / g250 / g400 | AI 스킬 만들기 | Teach a New Skill | AIにスキルを教える | 🟠 |
| `XX0014` | radial_action_variety | Tiered | 2 / 4 / 5 | g50 / g150 / g250 | 라디얼 메뉴 탐험 | Radial Menu Explorer | ラジアルメニュー探検 | 🟠 |
| `XX0015` | tray_menu_use | OneTime | 1 | g60 | 트레이에서 만나기 | Tray Icon Handshake | トレイからこんにちは | 🟠 |
| `XX0016` | chat_panel_resize | OneTime | 1 | g50 | 내 손으로 맞추기 | Resize It Your Way | 自分の手でサイズ調整 | 🟠 |
| `XX0017` | window_collision_toggle | OneTime | 1 | g50 | 창문 위에 앉기 | Perch on a Window | ウィンドウの上に座る | 🟠 |

_주요 훅: HotkeyManager.SetBinding(catalog, actionName, persist=true) — 저장된 바인딩이 기본값과 다를 때 Report("hotkey_customize") 호출. · HotKeyActionManager.Execute(actionName) 호출마다 HashSet<string>에 액션명 추가, 크기 증가 시 ReportBest("hotkey_action_variety", set.Count). · ClipboardManager.OnClipboardChanged() → ChatBalloonManager.SetLastImageSource("clipboard") 이후 실제 채팅 전송이 확인되는 지점에 Report("clipboard_ai_share") 추가. · ScreenshotManager.SaveAndShowScreenshot() 완료 콜백에서 Report("screenshot_capture", 1). · ScreenshotOCRManager.ExecuteOCRWithSlot(options, slot) 실행마다 옵션 조합을 HashSet에 기록, 종류 증가 시 ReportBest("ocr_combo_variety", set.Count). · InstallStatusManager.SetToLite()/SetToFull() 이후 GetInstallStatusIndex 값을 그대로 ReportBest("edition_upgrade", index)에 전달. · JarvisServerManager.CheckHealthAndNotify() 성공 → ScenarioCommonManager.Run_C01_ServerStarted() 진입 지점에 Report("server_first_connect") 추가. · ScenarioTutorialManager.Scenario_A99_ConfigEnd() 에서 settings.isTutorialCompleted=true 설정 시 Report("setup_tutorial_complete") 추가. · ScenarioTutorialManager.Scenario_A03_1_LocalCompute() 또는 Scenario_A04_2_APIKeyInput() 분기 진입 시 Report("compute_path_explorer"). · DownloadManager.RequestAddressableDownload(...) 성공 콜백에서 Report("dlc_download_complete", 1)._

---

## 합계 (신규)

| 코드 | 카테고리 | 기존 | 신규 |
|------|----------|-----:|-----:|
| OB | 첫걸음 | 8 | 18 |
| CV | 대화 | 9 | 58 |
| AF | 교감 | 5 | 87 |
| PR | 생활 | 8 | 88 |
| CH | 도전 | 8 | 19 |
| ST | 선톡(SmallTalk) | 0 | 20 |
| WB | 웹검색·지식 | 0 | 16 |
| AG | AI 에이전트 조작 | 0 | 18 |
| AC | 캐릭터 액션 | 0 | 15 |
| SK | AI 스킬 | 0 | 21 |
| MD | 대화 모드 | 0 | 19 |
| VC | 음성 | 0 | 20 |
| GM | 미니게임 | 0 | 17 |
| VS | 비전/화면 | 0 | 21 |
| WM | 창·표시 | 0 | 13 |
| HK | 핫키/입력 | 0 | 19 |
| NT | 알림/메뉴 | 0 | 20 |
| SE | 설정·언어·모델 | 0 | 21 |
| ME | 기억 | 0 | 25 |
| MR | MR/XR | 0 | 20 |
| XX | 기타(미분류) | 0 | 17 |

**신규 미션 합계: 572개** (기존 38개 별도).

---

## 검토 메모 / 다음 액션

### 탭 전략 (필수 결정)
- **옵션 C (권장)**: 기존 5탭 + **"비서(AI)" 탭 1개**로 ST/WB/AG/AC/SK/MD 흡수. 나머지(VC/GM/VS/WM/HK/NT/SE/ME/MR)는 성격상 CV(대화계)·PR(생활/도구계)로 매핑 → UI 재베이크 최소화, 정체성 강조.
- **옵션 A**: 카테고리별 탭 전면 확장(좌측 ScrollRect + `MissionCategory` enum 확장 + 재베이크).
- **옵션 B**: 5탭 유지, id 접두사만 신규로 두고 category는 5개 중 매핑.

### 우선순위
1. **🟢🟡 먼저** — 알람/할일/인벤토리/메타(이벤트 존재) + 대화/감정/선택지/챗모드/에이전트기능(실행 지점 명확, Report 한 줄).
2. **🟠 시스템 손볼 때 묶기** — 선톡 응답 판정, 포모도로 완료 이벤트, 친밀도 영속화, 플레이타임/접속일, 6감정 수집, STT 세션.
3. **🔴 카탈로그 대기** — 액세서리 구매/골드 대량 소비(상점·소비처 필요), OCR·MR 노출 확정 후.

### 밸런싱 미결
- 감정/키워드 카운트는 AI 응답(ai_info.emotion) 신뢰도에 좌우 → 흔들리면 목표치 완화.
- 시간대/요일/기념일 미션은 로컬시각 판정 규칙 확정 필요.
- 접속/플레이타임은 과거 카탈로그에서 삭제 이력 있음 → 재도입 여부 재확인.
- item1~3 정체 미확정 → 수집 미션 문구는 확정 후 다듬기.
- 보상 수치·Increment 점증은 초안값(현재 고정 정책).

### 다음 액션
1. 탭 전략(A/B/C) 결정 → 카테고리 매핑 확정.
2. 이 풀에서 채택 미션 선별(🟢🟡 우선) → `MissionDatabase.Build()`에 1줄씩(프리팹 재베이크 불필요).
3. 약한/중복 후보 정리 및 id 최종 재번호.
