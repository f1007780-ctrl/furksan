# iPro Sensor Panel - En Kolay EXE Çalıştırma

Aşağıdaki yöntemle Visual Studio veya terminalden **tek dosya EXE** üretebilirsiniz.

## 1) Gereksinim
- Windows 10/11
- .NET 8 SDK kurulu

Kontrol:
```bash
dotnet --version
```

## 2) Bu klasörde terminal aç
Proje kökü: `IProSensorPanel.csproj` dosyasının olduğu klasör.

## 3) Tek dosya EXE üret (önerilen)
Windows CMD için:
```bat
publish-win-x64.bat
```

Bu script şunu çalıştırır:
```bat
dotnet publish IProSensorPanel.csproj -c Release -r win-x64 --self-contained true /p:PublishSingleFile=true /p:IncludeNativeLibrariesForSelfExtract=true /p:PublishTrimmed=false
```

## 4) Çıkan EXE yolu
```text
bin\Release\net8.0-windows\win-x64\publish\IProSensorPanel.exe
```

## 5) Hızlı kullanım notları
- Uygulamayı çalıştırınca Ayarlar sekmesinden COM port seçip Bağlan'a basın.
- Arduino çıktısı şu formata uygun olmalı:
  - `S1:24.1;S2:25.0;S3:23.8;S4:26.2;S5:18.7`
- Loglar:
  - `%AppData%\IProSensorPanel\logs\YYYY-MM-DD.log`

## Sorun giderme
- `dotnet` bulunamıyorsa .NET 8 SDK kurun.
- COM port listesi boşsa USB sürücüsü/kablo ve Aygıt Yöneticisi kontrol edin.
- Kurumsal PC'de EXE engellenirse Windows Defender/SmartScreen izin verin.
