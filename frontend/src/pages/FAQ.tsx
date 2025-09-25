import React from 'react';
import { useTranslation } from 'react-i18next';

const FAQ: React.FC = () => {
  const { t } = useTranslation();

  return (
    <div className="max-w-4xl mx-auto px-4 py-10">
      <h1 className="text-3xl font-bold text-gray-900 mb-6">{t('faq.title')}</h1>

      <div className="space-y-6 text-gray-800">
        <div>
          <h2 className="text-xl font-semibold mb-2">{t('faq.whatTitle')}</h2>
          <p>{t('faq.whatText')}</p>
        </div>

        <div>
          <h2 className="text-xl font-semibold mb-2">{t('faq.moderationTitle')}</h2>
          <p>{t('faq.moderationText')}</p>
        </div>

        <div>
          <h2 className="text-xl font-semibold mb-2">{t('faq.privacyTitle')}</h2>
          <p>{t('faq.privacyText')}</p>
        </div>

        <div>
          <h2 className="text-xl font-semibold mb-2">{t('faq.whyReviewsTitle')}</h2>
          <p>{t('faq.whyReviewsText')}</p>
        </div>

        <div>
          <h2 className="text-xl font-semibold mb-2">{t('faq.reportTitle')}</h2>
          <p>{t('faq.reportText')}</p>
        </div>

        <div>
          <h2 className="text-xl font-semibold mb-2">{t('faq.joinTitle')}</h2>
          <p>{t('faq.joinText')}</p>
        </div>
      </div>
    </div>
  );
};

export default FAQ;