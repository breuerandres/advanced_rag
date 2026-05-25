import i18n from 'i18next';
import LanguageDetector from 'i18next-browser-languagedetector';
import { initReactI18next } from 'react-i18next';

import esAR from './es-AR.json';
import enUS from './en-US.json';
import ptBR from './pt-BR.json';

void i18n
  .use(LanguageDetector)
  .use(initReactI18next)
  .init({
    fallbackLng: 'es-AR',
    supportedLngs: ['es-AR', 'en-US', 'pt-BR'],
    interpolation: { escapeValue: false },
    resources: {
      'es-AR': { translation: esAR },
      'en-US': { translation: enUS },
      'pt-BR': { translation: ptBR },
    },
    detection: {
      order: ['querystring', 'cookie', 'localStorage'],
      caches: ['cookie', 'localStorage'],
      lookupQuerystring: 'lng',
      lookupCookie: 'helpcenter-locale',
      lookupLocalStorage: 'helpcenter:locale',
    },
  });

export default i18n;
