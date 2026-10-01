# نشر KeyFix

## إنشاء الحزم محلياً

من PowerShell داخل مجلد المشروع:

```powershell
.\build-packages.ps1
```

ينفذ السكربت الاستعادة والاختبارات والنشر، ثم ينشئ داخل `artifacts`:

- `KeyFix-<version>-win-x64.zip`: حزمة المستخدمين الجاهزة للتحميل.
- `KeyFix-<version>-source.zip`: نسخة مصدر نظيفة للنسخ الاحتياطي أو التسليم.
- `SHA256SUMS.txt`: بصمات التحقق من سلامة الحزم.

## الرفع إلى GitHub

المشروع يحتوي على Workflow يبني ويختبر التطبيق في Windows عند كل Push أو Pull Request.
عند إنشاء Tag مثل `v1.4.3` ينشئ Workflow إصدار GitHub ويرفع حزمة المستخدمين وحزمة المصدر وبصمة SHA-256 تلقائياً.

مثال بعد إنشاء مستودع GitHub وربطه:

```powershell
git add .
git commit -m "Release KeyFix 1.4.3"
git push origin main
git tag v1.4.3
git push origin v1.4.3
```

قبل إنشاء الـ Tag حدّث قسم `update` داخل `channel.json` بنفس رقم الإصدار ورابط الحزمة. راجع [دليل قناة GitHub](GITHUB-CHANNEL.md).

قبل جعل المستودع عاماً، اختر رخصة برمجية مناسبة وأضف ملف `LICENSE`. لم تُفرض رخصة تلقائياً لأن قرار الترخيص يعود لصاحب المشروع.
