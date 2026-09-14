import type { SupportedLocale } from "./localization";

const en = {
  "app.name": "SalekhPos",
  "common.retry": "Try again",
  "common.back": "Back",
  "common.loading": "Loading",
  "common.signOut": "Sign out",
  "error.title": "Something went wrong",
  "error.message": "The application could not continue safely.",
  "config.title": "Configuration required",
  "config.message": "Set a valid API environment and base URL before signing in.",
  "session.restoreError": "Your secure session could not be restored.",
  "signIn.title": "Secure sign in",
  "signIn.message": "Use your organization's approved identity provider to continue.",
  "signIn.pending": "Identity-provider sign in is not configured for this build.",
  "dashboard.title": "Dashboard",
  "dashboard.welcome": "Signed in as {name}",
  "dashboard.roles": "Assigned roles",
  "dashboard.noRoles": "No roles were supplied by the session.",
  "navigation.dashboard": "Dashboard",
  "navigation.scanner": "Barcode scanner",
  "scanner.title": "Barcode scanner",
  "scanner.explanation": "Camera access is requested only when you start scanning.",
  "scanner.start": "Start scanning",
  "scanner.permissionDenied": "Camera access was not granted.",
  "scanner.permissionSettings": "Camera access is disabled. Enable it in system settings.",
  "scanner.scanned": "Scanned barcode: {value}",
  "scanner.scanAgain": "Scan another barcode",
} as const;

export type TranslationKey = keyof typeof en;
type TranslationTable = Readonly<Record<TranslationKey, string>>;

const az: TranslationTable = {
  "app.name": "SalekhPos",
  "common.retry": "Yenidən cəhd edin",
  "common.back": "Geri",
  "common.loading": "Yüklənir",
  "common.signOut": "Çıxış",
  "error.title": "Xəta baş verdi",
  "error.message": "Tətbiq təhlükəsiz şəkildə davam edə bilmədi.",
  "config.title": "Konfiqurasiya tələb olunur",
  "config.message": "Daxil olmadan əvvəl düzgün API mühiti və əsas URL təyin edin.",
  "session.restoreError": "Təhlükəsiz sessiyanız bərpa edilə bilmədi.",
  "signIn.title": "Təhlükəsiz giriş",
  "signIn.message": "Davam etmək üçün təşkilatınızın təsdiqlənmiş giriş xidmətindən istifadə edin.",
  "signIn.pending": "Bu yığım üçün giriş xidməti konfiqurasiya edilməyib.",
  "dashboard.title": "İdarə paneli",
  "dashboard.welcome": "{name} kimi daxil olmusunuz",
  "dashboard.roles": "Təyin edilmiş rollar",
  "dashboard.noRoles": "Sessiya tərəfindən rol təqdim edilməyib.",
  "navigation.dashboard": "İdarə paneli",
  "navigation.scanner": "Barkod skaneri",
  "scanner.title": "Barkod skaneri",
  "scanner.explanation": "Kamera icazəsi yalnız skanı başladanda istənilir.",
  "scanner.start": "Skanı başladın",
  "scanner.permissionDenied": "Kamera icazəsi verilmədi.",
  "scanner.permissionSettings": "Kamera girişi deaktivdir. Onu sistem ayarlarında aktiv edin.",
  "scanner.scanned": "Skan edilmiş barkod: {value}",
  "scanner.scanAgain": "Başqa barkod skan edin",
};

const ka: TranslationTable = {
  "app.name": "SalekhPos",
  "common.retry": "ხელახლა ცდა",
  "common.back": "უკან",
  "common.loading": "იტვირთება",
  "common.signOut": "გასვლა",
  "error.title": "დაფიქსირდა შეცდომა",
  "error.message": "აპლიკაციამ უსაფრთხოდ გაგრძელება ვერ შეძლო.",
  "config.title": "საჭიროა კონფიგურაცია",
  "config.message": "შესვლამდე მიუთითეთ სწორი API გარემო და საბაზისო URL.",
  "session.restoreError": "თქვენი დაცული სესიის აღდგენა ვერ მოხერხდა.",
  "signIn.title": "უსაფრთხო შესვლა",
  "signIn.message": "გასაგრძელებლად გამოიყენეთ თქვენი ორგანიზაციის დამტკიცებული ავტორიზაცია.",
  "signIn.pending": "ამ ვერსიისთვის ავტორიზაციის პროვაიდერი კონფიგურირებული არ არის.",
  "dashboard.title": "მართვის პანელი",
  "dashboard.welcome": "შესული ხართ როგორც {name}",
  "dashboard.roles": "მინიჭებული როლები",
  "dashboard.noRoles": "სესიას როლები არ მოჰყოლია.",
  "navigation.dashboard": "მართვის პანელი",
  "navigation.scanner": "შტრიხკოდის სკანერი",
  "scanner.title": "შტრიხკოდის სკანერი",
  "scanner.explanation": "კამერის ნებართვა მოითხოვება მხოლოდ სკანირების დაწყებისას.",
  "scanner.start": "სკანირების დაწყება",
  "scanner.permissionDenied": "კამერის ნებართვა არ გაიცა.",
  "scanner.permissionSettings": "კამერაზე წვდომა გამორთულია. ჩართეთ სისტემის პარამეტრებში.",
  "scanner.scanned": "სკანირებული შტრიხკოდი: {value}",
  "scanner.scanAgain": "სხვა შტრიხკოდის სკანირება",
};

export const resources: Readonly<Record<SupportedLocale, TranslationTable>> = {
  en,
  az,
  ka,
};

export function translate(
  locale: SupportedLocale,
  key: TranslationKey,
  values: Readonly<Record<string, string>> = {},
): string {
  const template = resources[locale][key] ?? resources.en[key];
  return template.replace(/\{(\w+)\}/gu, (_, name: string) => values[name] ?? `{${name}}`);
}
