import { initReactI18next } from 'react-i18next';
import i18n from 'i18next';

// Подключаем ресурсы
import ru from './locales/ru/translation.json';
import en from './locales/en/translation.json';

// Детекция языка браузера: ru по умолчанию, если начинается с 'ru'
const browserLang = (navigator?.language || 'en').toLowerCase().startsWith('ru') ? 'ru' : 'en';

i18n
  .use(initReactI18next)
  .init({
    resources: {
      ru: { translation: ru },
      en: { translation: en }
    },
    lng: browserLang,
    fallbackLng: 'en',
    interpolation: { escapeValue: false },
    returnEmptyString: false,
  });

export default i18n;