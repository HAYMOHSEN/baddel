# بدّل | Baddel — مصحّح لغة الكتابة

تطبيق ويندوز يصحّح النص المكتوب بلغة لوحة المفاتيح الخطأ (عربي ⇄ إنجليزي) بضغطة واحدة، في أي برنامج.

مثال: تكتب `hgsghl ugd;l` لأن لوحة المفاتيح كانت على الإنجليزية، تحدّده وتضغط **Ctrl + Alt + Space**، فيصبح «السلام عليكم» وتتبدّل لغة لوحة المفاتيح إلى العربية لتكمل الكتابة.

## لماذا تطبيق ويندوز أصلي وليس PWA مثل التطبيقات السابقة؟

تطبيق الويب (PWA) لا يستطيع أن يلتقط اختصارًا من لوحة المفاتيح وأنت في برنامج آخر، ولا أن يقرأ النص المحدد في Word أو المتصفح ويستبدله. لذلك بُني «بدّل» بلغة C# على ‎.NET 10 وWPF، ويُغلَّف بصيغة MSIX عبر Visual Studio بدلًا من PWABuilder. المتجر يوقّع الحزمة مجانًا، فلا تحتاج إلى شهادة توقيع مدفوعة.

## ما تحتاجه (مرة واحدة)

1. **Visual Studio 2026 Community** (مجاني) من visualstudio.microsoft.com. عند التثبيت اختر:
   - من Workloads: **‎.NET desktop development**
   - من Individual components: **MSIX Packaging Tools** و**Windows 11 SDK (10.0.26100)**
2. حساب المطوّر في Partner Center (لديك من تطبيقاتك السابقة).

## ١) جرّبه على جهازك

1. افتح `Baddel.sln` في Visual Studio.
2. من الشريط العلوي اختر **Debug** و**x64**.
3. انقر بالزر الأيمن على مشروع **Baddel** واختر **Set as Startup Project**، ثم اضغط **F5**.
4. ستظهر نافذة الترحيب. جرّب داخلها، ثم افتح Notepad أو Word أو المتصفح، واكتب `hgsghl ugd;l`، وحدده واضغط **Ctrl + Alt + Space**.
5. جرّب أيضًا دون تحديد: اكتب `lvpfh hgdl` ثم اضغط الاختصار مباشرة؛ ستُصحَّح الكلمات الأخيرة في السطر. اضغط الاختصار مرة أخرى للتراجع.

لتشغيل اختبارات محرّك التحويل: **Test → Run All Tests** (أكثر من 70 اختبارًا).

## ٢) احجز الاسم في Partner Center

**Apps and games → New product → MSIX or PWA app**، واحجز:
- بالعربية: **بدّل - مصحّح لغة الكتابة**
- بالإنجليزية: **Baddel - Keyboard Language Fixer**

إذا كان الاسم محجوزًا، جرّب صيغة قريبة. الاسم داخل التطبيق يُعدَّل من `Resources/Strings.*.xaml` (المفتاح `App.Name`) ومن `Package.appxmanifest`.

## ٣) اربط المشروع بالمتجر

1. انقر بالزر الأيمن على مشروع **Baddel.Package** ← **Publish** ← **Associate App with the Store**.
2. سجّل الدخول بحساب المطوّر نفسه، واختر الاسم الذي حجزته، ثم **Associate**.

يملأ Visual Studio تلقائيًا هوية الحزمة (Name وPublisher) في `Package.appxmanifest`.

## ٤) أنشئ حزمة المتجر

1. بالزر الأيمن على **Baddel.Package** ← **Publish** ← **Create App Packages**.
2. اختر **Microsoft Store as [اسم التطبيق] by [اسمك]** ← **Next**.
3. فعّل المعماريتين **x64** و**ARM64** على **Release**، واترك **Generate app bundle: Always** ← **Create**.
4. يظهر ملف `.msixupload` في المجلد `packaging\Baddel.Package\AppPackages\`.
5. يُفضَّل أن تضغط **Launch Windows App Certification Kit** في الشاشة الأخيرة لتفحص الحزمة قبل الرفع.

## ٥) انشر صفحة سياسة الخصوصية

1. أنشئ مستودعًا عامًا على GitHub باسم **Baddel** (بالحرف B الكبير) تحت حسابك HAYMOHSEN.
2. ارفع المجلد `store/privacy` كما هو (ليصبح المسار `privacy/index.html`)، ومعه الملف `store/privacy/logo.png`.
3. فعّل **Settings ← Pages** على الفرع `main`.
4. سيكون الرابط: `https://haymohsen.github.io/Baddel/privacy/`، وهو الرابط نفسه المكتوب في `src/Baddel/AppInfo.cs`. إذا غيّرت المسار فعدّله هناك أيضًا.

## ٦) أرسل التطبيق في Partner Center

- **Pricing and availability:** السعر والأسواق.
- **Properties:** الفئة **Productivity**، ورابط سياسة الخصوصية، والبريد `haymohsen@gmail.com` للدعم.
- **Age ratings:** أجب بـ«لا» عن كل الأسئلة، فيحصل على تصنيف 3+.
- **Packages:** ارفع ملف `.msixupload`.
- **Store listings:** أضف العربية والإنجليزية، وانسخ النصوص من `store/listing-ar.md` و`store/listing-en.md`، وأضف لقطات الشاشة.
- **Submission options:** سيسألك عن الصلاحية `runFullTrust`؛ انسخ التبرير الموجود في `store/listing-en.md`.

بعد النشر، ضع رقم المتجر (Store ID) في `AppInfo.StoreProductId` في الإصدار التالي (اختياري).

## مشاكل شائعة

| المشكلة | الحل |
|---|---|
| `The Windows SDK version 10.0.26100.0 was not found` | بالزر الأيمن على Baddel.Package ← Properties، واختر Target version المثبّت لديك. |
| مشروع Baddel.Package يظهر **(unavailable)** | ثبّت المكوّن MSIX Packaging Tools من Visual Studio Installer. |
| الاختصار لا يعمل في برنامج معيّن | البرنامج يعمل بصلاحيات المسؤول؛ ويندوز يمنع البرامج العادية من الكتابة فيه. |
| الاختصار لا يعمل أبدًا | برنامج آخر يستخدمه؛ غيّره من تبويب الإعدادات. |
| لا يحدث تصحيح في نافذة الأوامر | مقصود: Ctrl+C هناك يوقف الأمر الجاري، فبدّل لا يعمل داخلها. |

السجلّ (للأخطاء فقط، ولا يحتوي أي نص كتبه المستخدم) في: `%LOCALAPPDATA%\Baddel\baddel.log`.

## هيكل المشروع

```
Baddel.sln
src/Baddel.Core/          محرّك التحويل (لا يعتمد على ويندوز، ومغطّى بالاختبارات)
src/Baddel/               تطبيق WPF: الاختصار، الحافظة، الأيقونة بجوار الساعة، النوافذ
  Services/               الخدمات (الاختصار، الحافظة، التخطيطات، التشغيل مع ويندوز، المتجر)
  Resources/              النصوص العربية والإنجليزية، والأنماط
packaging/Baddel.Package/ مشروع حزمة MSIX للمتجر، والأيقونات
tests/Baddel.Core.Tests/  اختبارات المحرّك
store/                    نصوص صفحة المتجر، وصفحة الخصوصية، وصور المتجر
```

## كيف يعمل (باختصار تقني)

- يسجّل اختصارًا واحدًا عبر `RegisterHotKey`، دون أي مراقبة للوحة المفاتيح.
- عند الضغط: ينسخ النص المحدد، ويحوّله محليًا، ويلصقه مكانه، ثم يعيد محتوى الحافظة كما كان (ويستثني النص المؤقت من سجلّ الحافظة).
- يقرأ تخطيطات لوحة المفاتيح المثبّتة فعلًا عبر `ToUnicodeEx`، فيدعم العربية 101 و102 وAZERTY وتخطيطات لاتينية غير الإنجليزية. وإذا لم يجد تخطيطًا عربيًا يستخدم جدول العربية 101 القياسي.
- يميّز «لا» بين `b` و`gh` بقائمة كلمات إنجليزية مشتقة من SCOWL (انظر `THIRD-PARTY-NOTICES.txt`).
