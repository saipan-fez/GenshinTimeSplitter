# Genshin Time Splitter CLI

GUI版をマルチプラットフォーム/コマンドラインから実行できるようにしたものです。

## ダウンロード/インストール

[GenshinTimeSplitter_CLI 最新版](https://github.com/saipan-fez/GenshinTimeSplitter/releases/latest/)

1. 上記リンクから`GenshinTimeSplitter_CLI_***` をダウンロード
1. ダウンロードしたzipファイルを任意のフォルダへ展開

!!! Note
    Windows PC で使用する場合: `win-x64`  
    Mac(Apple M1 CPU) で使用する場合: `osx-arm64`  
    Mac(Intel CPU) で使用する場合: `osx-x64`  

### Windows

特に追加で必要な手順はありません

### Mac

1. dotnet ランタイムをインストール
   ```sh
   wget https://dot.net/v1/dotnet-install.sh
   chmod +x dotnet-install.sh
   ./dotnet-install.sh --channel 8.0 --runtime dotnet
   ```
1. 依存パッケージをインストール
   ```sh
   brew install pkg-config mono-libgdiplus gtk+ ffmpeg@4 glog yasm harfbuzz jpeg libpng libtiff openjpeg metis openblas opencore-amr protobuf tbb webp # openexr
   ```
1. 実行権限を付与
   ```sh
   chmod +x GenshinTimeSplitterCLI
   ```
1. 実行ファイルに自己署名
   ```sh
   codesign -s - ./GenshinTimeSplitterCLI
   ```

### Linux

1. dotnet ランタイムをインストール
   ```sh
   wget https://dot.net/v1/dotnet-install.sh
   chmod +x dotnet-install.sh
   ./dotnet-install.sh --channel 8.0 --runtime dotnet
   ```
1. 依存パッケージをインストール
   ```sh
   sudo apt-get update -y 
   sudo apt-get install -y --no-install-recommends \
       apt-transport-https \
       software-properties-common \
       ca-certificates \
       g++ \
       make \
       cmake \
       libtbb-dev \
       libatlas-base-dev \
       libgtk2.0-dev \
       libavcodec-dev \
       libavformat-dev \
       libswscale-dev \
       libdc1394-dev \
       libxine2-dev \
       libv4l-dev \
       libtheora-dev \
       libvorbis-dev \
       libxvidcore-dev \
       libopencore-amrnb-dev \
       libopencore-amrwb-dev \
       x264 \
       libtesseract-dev
   sudo ldconfig
   ```
1. 実行権限を付与
   ```sh
   chmod +x GenshinTimeSplitterCLI
   ```

## 使用方法

!!! warning "重要"
    本アプリの仕組み上、解析には可能な限り高ビットレートの動画を使用してください。  
    低ビットレートの動画では、検知結果が誤ったものになる可能性が高まります。  

    **推奨 動画設定**  
    解像度: 1920x1080  
    フレームレート: 30fps  
    ビットレート: 10Mbps以上  

!!! Note
    動画が下記に該当する場合は `-r / --region` 引数の指定が必要です。  
    ・動画の上下または左右に黒帯（ゲーム画面外）がある  
    ・ゲーム画面にオーバーレイしてタイマーなどを表示している  

### コマンド例

#### デフォルトの設定で解析

Windows

```powershell
.\GenshinTimeSplitterCLI.exe -f sample.mp4
```

Mac/Linux

```sh
./GenshinTimeSplitterCLI -f sample.mp4
```

#### Regionを指定して解析

Windows

```powershell
.\GenshinTimeSplitterCLI.exe -f sample.mp4 -r '[{\"x\":405, \"y\":125, \"w\":150, \"h\":150},{\"x\":1365, \"y\":125, \"w\":150, \"h\":150},{\"x\":405, \"y\":375, \"w\":150, \"h\":150},{\"x\":1365, \"y\":375, \"w\":150, \"h\":150}]'
```

Mac/Linux

```sh
./GenshinTimeSplitterCLI -f sample.mp4 -r "[{\"x\":405, \"y\":125, \"w\":150, \"h\":150},{\"x\":1365, \"y\":125, \"w\":150, \"h\":150},{\"x\":405, \"y\":375, \"w\":150, \"h\":150},{\"x\":1365, \"y\":375, \"w\":150, \"h\":150}]"
```

#### 解析範囲を指定して解析

Windows

```powershell
.\GenshinTimeSplitterCLI.exe -f sample.mp4 -from 00:00:50 -to 00:48:29
```

Mac/Linux

```sh
./GenshinTimeSplitterCLI -f sample.mp4 -from 00:00:50 -to 00:48:29
```

### コマンド引数

#### -f / --file-path

解析する動画ファイルのファイルパス

#### -y

結果ファイルが存在する場合、強制的に上書きする

#### -from / --from-time

解析する開始時間を指定する。  
未指定の場合は動画ファイルの先頭から解析されます。

書式 `hh:mm:ss`

#### -to / --to-time

解析する終了時間を指定する。  
未指定の場合は動画ファイルの末尾まで解析されます。

書式 `hh:mm:ss`

#### -r / --regions

ロード画面として解析する区域。  
以下の場合は、ロード画面として判定する区域を変更する必要があります。

- 動画の上下または左右に黒帯（ゲーム画面外）がある  
- ゲーム画面にオーバーレイしてタイマーなどを表示している  

(Tips)  

設定する区域は、真っ白または真っ黒の部分です。

- [OK例](./img/setting_region_OK.drawio.png)
- [NG例](./img/setting_region_NG.drawio.png)

(Tips)  

矩形は上下左右4個所以上を指定することを推奨します。  
指定される矩形が少ない場合、誤検知する可能性が高まります。

(書式)  

- JSON Array
  - x: 矩形左上のX座標
  - y: 矩形左上のY座標
  - w: 矩形の幅
  - h: 矩形の高さ

```json
[
    {
        "x": 405,
        "y": 125,
        "w": 150,
        "h": 150
    },
    {
        "x": 1365,
        "y": 125,
        "w": 150,
        "h": 150
    },
    {
        "x": 405,
        "y": 375,
        "w": 150,
        "h": 150
    },
    {
        "x": 1365,
        "y": 375,
        "w": 150,
        "h": 150
    }
]
```

#### --diff-threashold

ロード画面として判定する閾値。  
値を大きくすると、低ビットレートの動画でも検知することが可能になります。  
ただし、誤検知する確率が増加するため、基本的には変更しないでください。

#### --false-detection-milli-seconds

ワープの誤検知と判断する時間(ミリ秒)。  
この数値より小さい時間をワープとして扱いません。  

##### ・ワープしていないのにワープしていると誤検知される場合

数字を大きくしてください。  
なお、設定値は最大でも`300`を推奨します。  
大きすぎると通常のワープを認識できなくなります。

##### ・ワープしているのにワープしていないと誤検知される場合

数字を小さくしてください。  
なお、設定値は最小でも`90`を推奨します。  
小さすぎるとワープしていないところをワープとして認識してしまいます。

#### --parallel-count

解析に使用するスレッド数。  
"0" の場合は全CPUコアが使用されます。  
基本的には変更しなくて問題ありません。

#### -out-movie / --output-section-movie

ワープ毎に分割した動画を出力するかどうか。  

!!! note
    動画を出力するためには [FFmpeg]([https://](https://ffmpeg.org/)) のインストールが必要です。

##### ・Disable

動画を出力しません

##### ・EnableNoEncode

動画は再エンコード無しで出力されます。  
そのため、短時間で作成できますが、開始数秒の映像が乱れることがあります。

##### ・EnableReEncode

動画は再エンコードされて出力されます。  
そのため、マシンパワーが必要かつ作成に時間を要しますが、映像が乱れることはありません。

#### -conf / --conf-file-path

以下のフォーマットの設定を読み込む。  
この引数が指定されている場合、該当する設定の引数値は無視されます。

```json
{
  "TargetMovieResolution": {
    "Width": 1920,
    "Height": 800
  },
  "DiffThreashold": 3,
  "FalseDetectionMilliSeconds": 200,
  "ParallelCount": 0,
  "OutputSectionMovie": "Disable",
  "AnalyzeRegions": [
    {
      "X": 405,
      "Y": 125,
      "Width": 150,
      "Height": 150
    },
    {
      "X": 1365,
      "Y": 125,
      "Width": 150,
      "Height": 150
    },
    {
      "X": 405,
      "Y": 375,
      "Width": 150,
      "Height": 150
    },
    {
      "X": 1365,
      "Y": 375,
      "Width": 150,
      "Height": 150
    }
  ]
}
```

## License

[MIT License](https://github.com/saipan-fez/GenshinTimeSplitter/blob/main/LICENSE)

This project is not affiliated with HoYoVerse.
Genshin Impact, game content and materials are trademarks and copyrights of HoYoVerse.