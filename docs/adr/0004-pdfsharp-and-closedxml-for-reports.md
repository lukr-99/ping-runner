# PDFsharp and ClosedXML for reports

Reports go to people who were not at the computer: a provider's support desk, a landlord, a colleague.
They need a PDF that looks the same everywhere and a workbook with real numbers. Ping Runner writes
both itself instead of printing a window, so a report never depends on the screen, the theme or a
printer driver. PDFsharp with MigraDoc (MIT) lays out the PDF with tables, page breaks and embedded
fonts; ClosedXML (MIT) writes and reads `.xlsx` without Excel installed. Both are pure .NET and add
about 12 MB to the installed app. QuestPDF, EPPlus and Syncfusion were ruled out because their terms
depend on who uses them and how much they earn, conditions that would pass on to everyone who gets Ping
Runner; printing a WPF `FlowDocument` to XPS and converting it gave too little control over the page.
PDFsharp reads fonts through a resolver; Ping Runner gives it Segoe UI from the Windows fonts folder,
with Arial as the fallback. Charts are drawn by the app and embedded as images,
because neither library draws charts the way the app does.
