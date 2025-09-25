import React, { useMemo, useState } from 'react';
import { useTranslation } from 'react-i18next';

type Level = 'Pending' | 'Green' | 'Yellow' | 'Red' | undefined;
type Source = 'LLM' | 'Manual' | 'None' | undefined;

function cls(level?: Level) {
  if (level === 'Red') return 'bg-red-100 text-red-800';
  if (level === 'Yellow') return 'bg-yellow-100 text-yellow-800';
  if (level === 'Green') return 'bg-green-100 text-green-800';
  return 'bg-gray-200 text-gray-700';
}

export const ReasonBadge: React.FC<{
  level?: Level;
  source?: Source;
  reason?: string;
  reasonRu?: string;
  reasonEn?: string;
  className?: string;
}> = ({ level, source, reason, reasonRu, reasonEn, className }) => {
  const [open, setOpen] = useState(false);
  const browserLang = (navigator?.language || 'en').toLowerCase();
  const { t } = useTranslation();

  const pick = useMemo(() => {
    if (reasonRu || reasonEn) {
      return browserLang.startsWith('ru') ? (reasonRu || reasonEn || '') : (reasonEn || reasonRu || '');
    }
    return reason || '';
  }, [reason, reasonRu, reasonEn, browserLang]);

  const srcLabel = source === 'LLM' ? t('reasonBadge.ai') || 'AI' : (source || t('reasonBadge.none') || '—');

  return (
    <span className={`inline-flex items-center ${className || ''}`}>
      <button
        type="button"
        onClick={() => setOpen(v => !v)}
        className={`text-xs px-2 py-0.5 rounded ${cls(level)} focus:outline-none`}
        aria-expanded={open}
        aria-label="Moderation info"
      >
        {level || 'Pending'}
      </button>
      {open && (
        <div className="ml-2 text-xs bg-white border border-gray-200 rounded shadow px-2 py-1 max-w-xs z-10">
          <div className="text-gray-700">
            <div className="mb-1"><span className="text-gray-500">{t('reasonBadge.source') || 'Source:'}</span> {srcLabel}</div>
            {pick ? <div className="text-gray-800 whitespace-pre-wrap">{pick}</div> : <div className="text-gray-400">{t('reasonBadge.noDetails') || 'No details'}</div>}
          </div>
        </div>
      )}
    </span>
  );
};