# Genshin Time Splitter

本アプリケーションはHoYoVerse社が提供する「原神」での精鋭狩りTA向けに、  
動画を解析し、ロード画面ごとに時間を区切った csv / json / xspf を出力します。  
出力したファイルを使用して、精鋭1体当たりの時間効率などを算出できます。  

![](./img/app.png)

## ダウンロード/インストール

Windows のみ
[GenshinTimeSplitter_x64.zip 最新版](https://github.com/saipan-fez/GenshinTimeSplitter/releases/latest/download/GenshinTimeSplitter_x64.zip)

CLI版(Win/Mac/Linux対応): [こちらのページ](./cli.md)

1. 上記リンクからダウンロード
1. ダウンロードしたzipファイルを任意のフォルダへ展開

アンインストールする場合は、フォルダを削除してください。

## Tutorial Video

TODO

## 使用方法

!!! warning "重要"
    本アプリの仕組み上、解析には可能な限り高ビットレートの動画を使用してください。  
    低ビットレートの動画では、検知結果が誤ったものになる可能性が高まります。  

    **推奨 動画設定**  
    解像度: 1920x1080  
    フレームレート: 30fps  
    ビットレート: 10Mbps以上  

!!! Note
    動画が下記に該当する場合は [[Analyze Setting] -> Region](#region) の設定が必要です。  
    ・動画の上下または左右に黒帯（ゲーム画面外）がある  
    ・ゲーム画面にオーバーレイしてタイマーなどを表示している  

### アプリケーションの使用方法

1. `GenshinTimeSplitter.exe` を起動
1. [Browse] ボタンから解析する動画を選択
1. `Analyze Range` で解析する動画時間を調整
1. `Analyze Setting` の設定値を変更 (下記参照)
1. [Start] ボタンを押下

結果は動画ファイルと同フォルダに出力されます。  
出力されたファイルの使い方は [出力ファイルの使用方法](./output_file_usage.md) を参照してください。

### Analyze Setting

#### Region

ロード画面として解析する区域。  
動画の上下または左右に黒帯（ゲーム画面外）がある、  
ゲーム画面にオーバーレイしてタイマーなどを表示している、  
などの場合は、ロード画面として判定する区域を調整してください。

設定する区域は、真っ白または真っ黒の部分です。

- [OK例](./img/setting_region_OK.drawio.png)
- [NG例](./img/setting_region_NG.drawio.png)

#### DiffThreshold

ロード画面として判定する閾値。  
値を大きくすると、低ビットレートの動画でも検知することが可能になります。  
ただし、誤検知する確率が増加するため、基本的には変更しないでください。

#### TheadNum

解析に使用するスレッド数。  
"0" の場合は全CPUコアが使用されます。  
基本的には変更しなくて問題ありません。

#### FalseDetection(ms)

ワープの誤検知と判断する時間(ミリ秒)。  
この数値より小さい時間をワープとして扱いません。  
  
##### ワープしていないのにワープしていると誤検知される場合

数字を大きくしてください。  
なお、設定値は最大でも`300`を推奨します。  
大きすぎると通常のワープを認識できなくなります。

##### ワープしているのにワープしていないと誤検知される場合

数字を小さくしてください。
なお、設定値は最小でも`90`を推奨します。  
小さすぎるとワープしていないところをワープとして認識してしまいます。

## 追加予定機能

- マップを開いたタイミングでの分割

## License

[MIT License](https://github.com/saipan-fez/GenshinTimeSplitter/blob/main/LICENSE)

This project is not affiliated with HoYoVerse.
Genshin Impact, game content and materials are trademarks and copyrights of HoYoVerse.