# ZOA — 独立桌宠 · Standalone Desktop Pet · 독립형 데스크톱 펫

[中文](#中文) · [English](#english) · [한국어](#한국어)

## 中文

ZOA 是一只适用于 Windows 的透明桌宠，由 Codex 版 ZOA 的动画资源改造而来。它可以独立运行，无需安装或打开 Codex，也不需要账号或网络连接。

### 下载与运行

1. 在 GitHub 页面选择 **Code → Download ZIP**，下载并解压整个仓库。
2. 安装 [Microsoft .NET 8 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/8.0)（如果尚未安装）。本项目提供的是依赖运行时的 Windows 程序。
3. 双击 `app/ZOA.exe`。请将 `app` 中的其他文件与 EXE 放在一起。

### 互动方式

| 操作或状态 | ZOA 的反应 |
| --- | --- |
| 向左或向右拖动 | 朝拖动方向跑动；左右跑动只在拖动时出现 |
| 单击 / 双击 | 挥手 / 跳跃 |
| 在任意程序中打字 | 循环播放思考动作；停止打字约 1.6 秒后结束 |
| 鼠标靠近 | 看向指针，暂停定时随机动作，并显示右上角的“大小”按钮 |
| 点击“大小” | 可选 75%、100%、125% 或 150%；鼠标远离后按钮隐藏 |
| 平时空闲 | 播放待机眨眼动作，约每 5 秒眨一次 |
| 约每分钟一次 | 随机播放挥手、跳跃、失落、思考或检查成果；采用洗牌轮播，不包含左右跑动 |
| 右击 ZOA 或托盘图标 | 调整大小、手动触发动作或退出 |

程序不会抢走其他窗口的键盘焦点。它通过全局键盘监听判断是否正在打字，**不保存按键内容**。位置和大小仅保存在本机的 `app/settings.json`。

### 文件与源码

- `app/`：运行所需的程序与精灵图。`settings.json` 是运行后产生的个人设置，上传或分享时请排除。
- `ZOA.Desktop/`：C# / Windows Forms 源码、精灵图和图标。原始 WebP 保留在这里；发布使用的 PNG 已将从上往下第 7 行“等待”动作清成透明。
- `.gitignore`：排除个人设置和编译缓存。

安装 [.NET SDK 8 或更新版本](https://dotnet.microsoft.com/download) 后，可在仓库根目录重新编译：

```powershell
dotnet publish .\ZOA.Desktop\ZOA.Desktop.csproj -c Release -o .\app
```

## English

ZOA is a transparent desktop pet for Windows, adapted from the animated ZOA pet used in Codex. It runs independently: Codex, an account, and an internet connection are not required.

### Download and run

1. On GitHub, choose **Code → Download ZIP** and extract the whole repository.
2. Install the [Microsoft .NET 8 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/8.0) if you do not already have it. The included Windows build requires this runtime.
3. Double-click `app/ZOA.exe`. Keep the other files in `app` beside the EXE.

### Interactions

| Action or state | ZOA's response |
| --- | --- |
| Drag left or right | Run in that direction; running animations appear only while dragging |
| Single-click / double-click | Wave / jump |
| Type in any application | Loop the thinking animation until about 1.6 seconds after typing stops |
| Move the pointer near ZOA | Look toward the pointer, pause scheduled random actions, and show the Size button at the upper right |
| Click Size | Choose 75%, 100%, 125%, or 150%; the button hides when the pointer moves away |
| Normally idle | Play the idle blink animation about once every 5 seconds |
| About once a minute | Play a shuffled random action: wave, jump, sad reaction, thinking, or review; left and right running are excluded |
| Right-click ZOA or its tray icon | Change size, trigger an action manually, or exit |

ZOA does not take keyboard focus from other windows. A global keyboard hook detects typing activity but **does not store keystrokes**. Position and size are saved locally in `app/settings.json`.

### Files and source

- `app/`: the runnable program and sprite sheet. `settings.json` contains personal settings created at runtime; leave it out when publishing or sharing.
- `ZOA.Desktop/`: the C# / Windows Forms source, sprite sheets, and icon. The original WebP is retained here. Row 7 of the distributed PNG, the waiting animation, has been cleared to transparency.
- `.gitignore`: excludes personal settings and build output.

With the [.NET SDK 8 or newer](https://dotnet.microsoft.com/download), rebuild from the repository root:

```powershell
dotnet publish .\ZOA.Desktop\ZOA.Desktop.csproj -c Release -o .\app
```

## 한국어

ZOA는 Codex에서 사용하던 ZOA 애니메이션을 바탕으로 만든 Windows용 투명 데스크톱 펫입니다. Codex 설치나 실행, 계정, 인터넷 연결 없이 독립적으로 사용할 수 있습니다.

### 다운로드 및 실행

1. GitHub에서 **Code → Download ZIP**을 선택하고 저장소 전체의 압축을 풉니다.
2. 설치되어 있지 않다면 [Microsoft .NET 8 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/8.0)을 설치합니다. 포함된 Windows 실행 파일에는 이 런타임이 필요합니다.
3. `app/ZOA.exe`를 더블클릭합니다. `app` 폴더의 다른 파일도 EXE와 같은 위치에 두세요.

### 조작 방법

| 조작 또는 상태 | ZOA의 반응 |
| --- | --- |
| 왼쪽 또는 오른쪽으로 드래그 | 드래그 방향으로 달립니다. 달리기 동작은 드래그할 때만 나타납니다 |
| 한 번 클릭 / 두 번 클릭 | 손 흔들기 / 점프 |
| 어느 프로그램에서든 타이핑 | 생각 동작을 반복하다가 입력이 멈춘 뒤 약 1.6초 후 종료됩니다 |
| 마우스 포인터가 가까워짐 | 포인터를 바라보고 정기 무작위 동작을 멈추며 오른쪽 위에 크기 버튼을 표시합니다 |
| 크기 버튼 클릭 | 75%, 100%, 125%, 150% 중 선택합니다. 포인터가 멀어지면 버튼이 숨겨집니다 |
| 평소 대기 상태 | 약 5초마다 대기 상태의 눈 깜빡임 동작을 재생합니다 |
| 약 1분마다 | 손 흔들기, 점프, 시무룩한 반응, 생각, 결과 확인 중 하나를 섞인 순서로 재생합니다. 좌우 달리기는 제외됩니다 |
| ZOA 또는 트레이 아이콘을 오른쪽 클릭 | 크기 조절, 동작 직접 실행 또는 종료 |

ZOA는 다른 창의 키보드 포커스를 빼앗지 않습니다. 전역 키보드 후크로 타이핑 여부만 감지하며 **입력 내용은 저장하지 않습니다**. 위치와 크기는 로컬 `app/settings.json`에 저장됩니다.

### 파일과 소스 코드

- `app/`: 실행 파일과 스프라이트 시트가 있습니다. `settings.json`은 실행 중 생성되는 개인 설정이므로 공개하거나 공유할 때 제외하세요.
- `ZOA.Desktop/`: C# / Windows Forms 소스 코드, 스프라이트 시트, 아이콘이 있습니다. 원본 WebP는 여기에 보관됩니다. 배포용 PNG의 위에서 일곱 번째 줄에 있던 기다림 동작은 투명하게 처리했습니다.
- `.gitignore`: 개인 설정과 빌드 결과물을 제외합니다.

[.NET SDK 8 이상](https://dotnet.microsoft.com/download)을 설치한 뒤 저장소 최상위 폴더에서 다시 빌드할 수 있습니다.

```powershell
dotnet publish .\ZOA.Desktop\ZOA.Desktop.csproj -c Release -o .\app
```
