# NFC Lens for Windows

The Android app lives in its own repository, and the protocol core is the shared Core repository at `Core/`.

Windows Forms card inspector targeting .NET 10 on Windows.

## NFC tags and EMV cards

**Read card** uses **Auto (EMV first)** by default: recognised PC/SC storage-card ATRs go to their dedicated NFC path; other cards run the original EMV scan first. **EMV only** uses the original EMV commands exclusively, without UID probes, NFC file selection or Classic key loading.

- Type 2 NDEF tags (NTAG213/215/216 and compatible Ultralight layouts), through PC/SC readers supporting ACS-style `FF CA` UID and `FF B0` four-byte page reads.
- Type 4 NDEF tags with mapping version 2 and standard NDEF file control TLVs. Reads the advertised file identifier and respects its read limit; message size is capped at 8 KiB.
- EMV applications through the existing directory/application/record flow.
- MIFARE Classic-compatible 1K, 4K and Mini cards identified by their PC/SC storage ATR: sector authentication followed by 16-byte data-block reads. Sector trailers are skipped. Both Key A and Key B are selectable.

### MIFARE Classic keys

Open **Classic key…** to enter one known six-byte key as 12 hexadecimal characters, selecting Key A or B. The initial setting is factory-default `FFFFFFFFFFFF`, Key A. The selected key is used once per sector; there is no dictionary search. Keys stay in application memory for the session and are loaded into volatile reader slot 0. Key-load bytes are always redacted in the transcript, regardless of reveal settings. Classic settings do not affect EMV reads.

Failed sector authentication or block access is reported alongside readable sectors. Classic blocks are shown as hex and printable ASCII; proprietary layouts and Classic NDEF/MAD mapping are not decoded automatically. A factory key only works where the card still uses it. Different sectors can require different keys; change the setting and rescan if you know the appropriate key. No card-memory writes or key changes are performed.

NDEF Text and URI records are decoded; other unchunked record payloads appear as hexadecimal. **Load demo** includes fictional NFC text and URI examples as well as an EMV record. Enable **Reveal full card data** to see identifiers and tag payloads. Stored URLs are displayed as text and never opened.

These operations never write card memory. Protected DESFire files are not authenticated or decrypted. Type 1/3/5, extended Type 4 files, chunked NDEF, Type 2 sector switching and reserved-memory layouts inside the data area are not supported. An accessible UID can still be shown for otherwise unsupported cards. Driver and reader capabilities affect which probes work. Malformed or inaccessible data is reported under **Read status**, with exchanges retained in **Live log**.

Implementation references: [ACS ACR122U API](https://www.acs.com.hk/download-manual/419/API-ACR122U-2.03.pdf), [NXP NTAG data sheet](https://www.nxp.com/docs/en/data-sheet/NTAG213_215_216.pdf), [TI Type 4 application report](https://www.ti.com/lit/pdf/sloa208), and [NFC Forum NDEF introduction](https://nfc-forum.org/uploads/specifications/NFCForum-AD-CPUX-1.1.pdf).

## Open and edit the form

Open `NfcLens.sln` in Visual Studio with the **.NET desktop development** workload and .NET 10 support installed. In Solution Explorer, right-click `MainWindow.cs` and choose **View Designer** (Shift+F7). The layout is in `MainWindow.Designer.cs`; event handling and scan behaviour are in `MainWindow.cs`. Reader access happens only at runtime.

In Visual Studio Code, open this folder to browse and edit these files, and run the commands below in its terminal. VS Code does not include Microsoft's Visual Studio Windows Forms designer; use Visual Studio for that design surface.

## Build and run

```powershell
dotnet build NfcLens.sln
dotnet run --project NfcLens.csproj
dotnet run --project NfcLens.csproj -- --self-test
```

## Learn as you scan

Enable **Learning mode** to unlock the **Learn** page. Select a lesson from the history for explanations and questions. New commands and responses add lessons while **Read card** runs. Lessons remain available after the scan, and enabling learning after a scan interprets its existing transcript.

Use **Practice walkthrough (no card)** for labelled examples of SELECT, GPO, READ RECORD, GET RESPONSE and status words. Practice is a static teaching sequence, not a connected-card session. **Load demo** also opens practice when learning is enabled.

Select a tagged field in **Card details** to open its explanation. **Live log** retains timestamped traffic; reveal controls apply to the log and exported report. Learning notes never copy cardholder values or response bodies. Learning does not issue additional APDUs or perform payments. Lessons cover this inspector's EMV flow, not all NFC protocols or radio frames.

## Reference material

- [EMVCo contactless overview](https://www.emvco.com/emv-technologies/emv-contactless-chip/)
- [Oracle APDU communication reference](https://docs.oracle.com/javame/config/cldc/opt-pkgs/api/security/satsa-api/jsr177/javax/microedition/apdu/APDUConnection.html)
- [Microsoft Windows Forms designer](https://learn.microsoft.com/en-us/visualstudio/designers/windows-forms-designer-overview)
