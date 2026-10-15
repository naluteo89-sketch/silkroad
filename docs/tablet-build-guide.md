# 태블릿 빌드 가이드 (뮤패드 K10 PLUS)

> Unity 프로젝트 → 안드로이드 앱(apk) → 태블릿 설치까지 순서대로.
> 처음 한 번은 30분~1시간 걸리고, 이후 업데이트는 5~10분 정도 걸림.
> 체크 항목 전체는 `docs/tablet-release-checklist.md` 참고.

---

## 0. 처음 한 번만 준비

### PC (Unity)

1. **Unity Hub → 설치 → 6000.3 버전 옆 ⚙ → 모듈 추가**
2. **Android Build Support** 체크 + 그 아래 **OpenJDK**, **Android SDK & NDK Tools** 도 체크 → 설치
   - 이미 설치되어 있으면 "설치됨" 으로 표시됨
3. 데이터 전송이 되는 USB 케이블 준비 (충전 전용 케이블은 태블릿이 PC 에서 안 보임)

### 태블릿 (개발자 모드 + USB 디버깅)

1. **설정 → 태블릿 정보**(또는 휴대전화 정보 / 소프트웨어 정보) → **빌드 번호** 7번 연속 터치 → "개발자가 되었습니다"
2. **설정 → 시스템 → 개발자 옵션** → **USB 디버깅 ON**
3. 같은 화면에서 **화면 켜짐 상태 유지** ON (충전 중 화면 꺼짐 방지, 체험용에 편함)
4. 태블릿 안드로이드 버전 확인: 설정 → 태블릿 정보 → **Android 버전** (메모해 두기)

---

## 1. 빌드 전 확인 (매번)

| # | 할 일 | 확인 |
|---|---|---|
| 1 | ▶ 실행 중이면 멈추기 | |
| 2 | `unity\Assets\StreamingAssets\ComfyUI\` 에 최종 워크플로·프리셋 JSON 이 들어 있는지 | |
| 3 | Hierarchy **ComfyUI (설정)** → **Use Dummy 해제**, Server Url `https://cloud.comfy.org`, **Api Key 칸은 비움** | |
| 4 | 같은 Inspector 에서 **Key Status ✅** (`C:\keydata\comfy_key.py` 를 읽었는지) | |
| 5 | Workflow Path / Preset Path 가 쓰려는 파일인지 | |
| 6 | 에디터에서 ▶ 한 번 돌려 보기 → 그림 → 보내기 → 결과 나오는지 | |
| 7 | Console 빨간 오류 0개 | |
| 8 | **Ctrl+S** 로 씬 저장 | |

> 🔑 API 키는 빌드할 때만 apk 안에 자동으로 들어가고, 빌드가 끝나면 프로젝트 폴더에서 자동으로 지워짐. 직접 넣거나 지울 필요 없음.

---

## 2. 안드로이드로 전환 (처음 한 번)

1. **File → Build Profiles**
2. 왼쪽 목록에서 **Android** 선택 → **Switch Platform**
   - 처음엔 모든 이미지를 다시 변환해서 몇 분 걸림. 끝날 때까지 기다리기
3. **Scene List** 에 `Scenes/Main` 이 체크되어 있는지 확인
   - 없으면 Project 창에서 `Assets/_Project/Scenes/Main.unity` 를 목록으로 끌어넣기
4. 메뉴 **Silkroad → 안드로이드 태블릿 설정 적용**
   - 가로 화면 고정, IL2CPP / ARM64, http 허용, 앱 이름 자동 설정

---

## 3. Player Settings (처음 한 번 + 버전만 매번)

**Build Profiles 창 → Player Settings** (또는 Edit → Project Settings → Player)

| 항목 | 값 | 비고 |
|---|---|---|
| Company Name | `Witches` | 영문 |
| Product Name | 태블릿에 보일 앱 이름 | 한글 가능 (현재 "실크로드 그림 공방") |
| Icon | 512×512 PNG | 아트 나오면 적용 |
| Other Settings → **Package Name** | `com.witches.silkroad` | **Override Default Package Name** 체크 후 입력. 앱 이름이 한글이면 자동 이름이 오류 날 수 있음. **한 번 정하면 바꾸지 말 것** |
| Other Settings → **Version** | `0.1.0` → `0.1.1` … | 사람이 보는 버전 |
| Other Settings → **Bundle Version Code** | **빌드할 때마다 +1** | 낮거나 같으면 업데이트 설치가 거부됨 |
| Other Settings → Minimum API Level | 기본값 유지 | 태블릿 안드로이드 버전보다 높으면 설치 불가 → 그때만 낮춤 |

---

## 4. 빌드하고 태블릿에 설치

### 방법 A. USB 로 바로 설치 (권장)

1. 태블릿을 USB 로 PC 에 연결
2. 태블릿에 **"USB 디버깅을 허용하시겠습니까?"** → **이 컴퓨터에서 항상 허용** 체크 → 허용
3. Build Profiles → **Run Device** 옆 **Refresh** → 목록에서 태블릿 선택
4. **Build And Run** 클릭
5. 저장 위치: `C:\silkroad\unity\Builds\` 폴더 만들고 파일 이름 `silkroad_0.1.0.apk`
   - `Builds` 폴더와 apk 는 깃에 올라가지 않게 이미 설정되어 있음 (**apk 안에 API 키가 있으니 공유 금지**)
6. 빌드 완료 → 태블릿에서 앱이 자동으로 실행됨

### 방법 B. apk 파일만 옮겨서 설치 (USB 디버깅 없이)

1. Build Profiles → **Build** 로 apk 만 만들기
2. apk 를 태블릿으로 복사 (USB 파일 전송 / 구글 드라이브 등)
3. 태블릿의 **내 파일** 에서 apk 터치 → "출처를 알 수 없는 앱" 허용 → 설치

---

## 5. 태블릿에서 처음 실행 후 확인

1. 시작 화면의 **설정** → 서버 주소가 `https://cloud.comfy.org` 인지, **더미 모드 해제**인지 확인 → **연결 테스트** → **저장**
   - 앱 안 설정은 태블릿에 저장되고 Inspector 값보다 우선함
2. Wi-Fi 연결 후 그림 → 보내기 → 결과 → 저장까지 한 번 해 보기
3. 결과 파일 위치: 태블릿 `Android/data/com.witches.silkroad/files/Results/`
4. 나머지 실기기 테스트는 `tablet-release-checklist.md` D 항목

---

## 6. 다음부터 업데이트할 때

1. 1번(빌드 전 확인) 다시 하기
2. **Bundle Version Code +1** (Version 도 올리기)
3. **Build And Run** → 같은 이름이면 덮어써서 업데이트됨 (태블릿 설정·저장 결과 유지)

> ⚠️ **다른 PC 에서 빌드하면** 서명이 달라서 업데이트 설치가 거부됨 → 태블릿에서 앱을 지우고 다시 설치(설정은 다시 입력).
> 행사용으로 확정되면 Player Settings → Publishing Settings 에서 **Keystore** 를 만들어 고정. Keystore 파일과 비밀번호는 잃어버리면 복구 불가, 깃에 올리지 말 것.

---

## 막히면

| 증상 | 해결 |
|---|---|
| Android 항목이 회색 / "Android SDK not found" | 0번: Unity Hub 에서 Android Build Support 모듈 설치 |
| Run Device 에 태블릿이 안 보임 | 데이터 케이블인지 확인, 태블릿 알림에서 USB 모드를 **파일 전송**으로, USB 디버깅 허용 팝업 확인 후 Refresh. 그래도 안 되면 다른 USB 포트 |
| "Package name … is not valid" | 3번: Override Default Package Name 체크 → `com.witches.silkroad` |
| "INSTALL_FAILED_VERSION_DOWNGRADE" / 업데이트 실패 | Bundle Version Code 올리기 |
| "INSTALL_FAILED_UPDATE_INCOMPATIBLE" / 서명 다름 | 태블릿에서 앱 삭제 후 다시 설치 |
| "Gradle build failed" | Console 전체 내용 캡처해서 Claude 에게 |
| 앱은 켜지는데 생성 실패 | Wi-Fi 연결, 설정 창 더미 해제·주소 확인, 빌드 Console 에 "[Silkroad] 빌드에 API 키 포함" 문구가 있었는지 |
| 글자가 □ 로 나옴 | 폰트 문제 → 해당 화면 캡처해서 전달 |
| 화면이 세로로 나옴 / 잘림 | 메뉴 **Silkroad → 안드로이드 태블릿 설정 적용** 다시 → 재빌드 |
