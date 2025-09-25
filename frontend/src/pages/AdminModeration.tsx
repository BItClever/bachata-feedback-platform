import React, { useEffect, useMemo, useState } from 'react';
import { moderationAdminAPI, adminReportsAPI } from '../services/api';
import { ReasonBadge } from '../components/ReasonBadge';
import { useAuth } from '../contexts/AuthContext';
import { useTranslation } from 'react-i18next';

type Level = 'Pending' | 'Green' | 'Yellow' | 'Red';
type ActionLevel = Exclude<Level, 'Pending'>;

type ItemBase = {
  id: number;
  type: 'Review' | 'EventReview';
  createdAt: string;
  reviewerName: string;
  moderationLevel: Level;
  moderationSource?: 'LLM' | 'Manual' | 'None';
  moderatedAt?: string;
  reason?: string;
  reasonRu?: string;
  reasonEn?: string;
  text?: string | null;
  reportsCount?: number;
};
type UserReviewItem = ItemBase & {
  type: 'Review';
  revieweeName: string;
  eventName?: string | null;
  leadRatings?: Record<string, number> | null;
  followRatings?: Record<string, number> | null;
};
type EventReviewItem = ItemBase & {
  type: 'EventReview';
  eventName: string;
  ratings?: Record<string, number> | null;
};
type AnyItem = UserReviewItem | EventReviewItem;

type PhotoReport = {
  id: number;
  targetId: number;
  targetType: 'UserPhoto' | 'EventPhoto' | 'Photo';
  reporterName: string;
  reason: string;
  createdAt: string;
  preview?: { kind: 'User' | 'Event'; smallUrl: string; mediumUrl: string; largeUrl: string };
};

const AdminModeration: React.FC = () => {
  const { user } = useAuth();
  const { t } = useTranslation();
  const canSee = !!user?.roles?.some(r => r === 'Admin' || r === 'Moderator');
  const [status, setStatus] = useState<Level | 'All'>('Pending');
  const [search, setSearch] = useState('');
  const [items, setItems] = useState<AnyItem[]>([]);
  const [error, setError] = useState('');
  const [busy, setBusy] = useState(false);
  const [expanded, setExpanded] = useState<Record<string, boolean>>({});
  const [reports, setReports] = useState<Record<string, any[]>>({});
  const [photoReports, setPhotoReports] = useState<PhotoReport[]>([]);
  const [photosLoading, setPhotosLoading] = useState<boolean>(false);

  const load = async () => {
    setError('');
    try {
      const [r1, r2] = await Promise.all([
        moderationAdminAPI.listUserReviews({ status, search, take: 100 }),
        moderationAdminAPI.listEventReviews({ status, search, take: 100 }),
      ]);
      const uitems: UserReviewItem[] = r1.data.map((x: any) => ({
        id: x.id, type: 'Review', createdAt: x.createdAt,
        reviewerName: x.reviewerName, revieweeName: x.revieweeName, eventName: x.eventName,
        text: x.text, leadRatings: x.leadRatings || null, followRatings: x.followRatings || null,
        moderationLevel: x.moderationLevel, moderationSource: x.moderationSource,
        moderatedAt: x.moderatedAt, reason: x.reason, reasonRu: x.reasonRu, reasonEn: x.reasonEn,
        reportsCount: x.reportsCount || 0,
      }));
      const eitems: EventReviewItem[] = r2.data.map((x: any) => ({
        id: x.id, type: 'EventReview', createdAt: x.createdAt,
        reviewerName: x.reviewerName, eventName: x.eventName,
        text: x.text, ratings: x.ratings || null,
        moderationLevel: x.moderationLevel, moderationSource: x.moderationSource,
        moderatedAt: x.moderatedAt, reason: x.reason, reasonRu: x.reasonRu, reasonEn: x.reasonEn,
        reportsCount: x.reportsCount || 0,
      }));
      const all = [...uitems, ...eitems].sort((a, b) => +new Date(b.createdAt) - +new Date(a.createdAt));
      setItems(all);
    } catch (e: any) {
      setError(e?.response?.data?.message || t('common.error') || 'Failed to load');
    }
  };

  useEffect(() => { load(); /* eslint-disable-next-line */ }, [status]);
  useEffect(() => {
    const id = setTimeout(() => load(), 300);
    return () => clearTimeout(id);
    // eslint-disable-next-line
  }, [search]);
  useEffect(() => { loadPhotoReports(); }, []);

  const toggle = (k: string) => setExpanded(prev => ({ ...prev, [k]: !prev[k] }));

  const loadReports = async (type: 'Review' | 'EventReview', id: number) => {
    const key = `${type}:${id}`;
    if (reports[key]) return;
    try {
      const r = await moderationAdminAPI.getReportsByTarget(type, id);
      setReports(prev => ({ ...prev, [key]: r.data }));
    } catch { /* ignore */ }
  };

  const setLevel = async (it: AnyItem, level: ActionLevel, reason?: string, reasonRu?: string, reasonEn?: string) => {
    setBusy(true);
    try {
      if (it.type === 'Review') await moderationAdminAPI.setReviewLevel(it.id, level, reason, reason, reason);
      else await moderationAdminAPI.setEventReviewLevel(it.id, level, reason, reason, reason);
      await load();
    } catch (e: any) {
      setError(e?.response?.data?.message || t('common.error') || 'Failed to set level');
    } finally {
      setBusy(false);
    }
  };

  const requeue = async (it: AnyItem) => {
    setBusy(true);
    try {
      await moderationAdminAPI.requeue(it.type, it.id);
      await load();
    } catch (e: any) {
      setError(e?.response?.data?.message || t('common.error') || 'Failed to requeue');
    } finally {
      setBusy(false);
    }
  };

  const loadPhotoReports = async () => {
    try {
      setPhotosLoading(true);
      const r = await adminReportsAPI.list('Pending');
      const items: PhotoReport[] = await Promise.all(r.data.map(async (x) => {
        const targetType = (x.targetType as 'UserPhoto' | 'EventPhoto' | 'Photo');
        try {
          const kind = targetType === 'Photo' ? undefined : targetType;
          const info = await moderationAdminAPI.photoInfo(x.targetId, kind as any);
          return {
            id: x.id,
            targetId: x.targetId,
            targetType,
            reporterName: x.reporterName,
            reason: x.reason,
            createdAt: x.createdAt,
            preview: { kind: info.data.kind as any, smallUrl: info.data.smallUrl, mediumUrl: info.data.mediumUrl, largeUrl: info.data.largeUrl }
          };
        } catch {
          return {
            id: x.id,
            targetId: x.targetId,
            targetType,
            reporterName: x.reporterName,
            reason: x.reason,
            createdAt: x.createdAt,
          };
        }
      }));
      setPhotoReports(items);
    } catch {
      // silent
    } finally {
      setPhotosLoading(false);
    }
  };

  const resolvePhotoReport = async (reportId: number, deleteTarget: boolean) => {
    setBusy(true);
    try {
      await adminReportsAPI.resolve(reportId, deleteTarget);
      await loadPhotoReports();
    } catch (e: any) {
      setError(e?.response?.data?.message || t('common.error') || 'Failed to resolve photo report');
    } finally {
      setBusy(false);
    }
  };

  const filtered = useMemo(() => items, [items]);

  if (!canSee) return <div className="p-8">{t('errors.accessDenied')}</div>;

  return (
    <div className="max-w-7xl mx-auto px-4 py-8">
      <h1 className="text-2xl font-bold mb-4">{t('adminModeration.title')}</h1>

      <div className="bg-white rounded shadow p-4 mb-4 flex flex-col md:flex-row gap-3 md:items-center md:justify-between">
        <div className="flex items-center gap-2">
          <label className="text-sm">{t('adminModeration.status')}</label>
          <select className="input-field" value={status} onChange={(e) => setStatus(e.target.value as any)}>
            {['Pending', 'Yellow', 'Red', 'Green', 'All'].map(s => <option key={s} value={s}>{s}</option>)}
          </select>
        </div>
        <div className="flex items-center gap-2">
          <input className="input-field" placeholder={t('adminModeration.searchPlaceholder') || 'Search...'} value={search} onChange={(e) => setSearch(e.target.value)} />
        </div>
      </div>

      {error && <div className="mb-4 bg-red-50 border border-red-200 text-red-800 px-4 py-2 rounded">{error}</div>}

      <div className="bg-white rounded shadow divide-y">
        {filtered.map((it) => {
          const key = `${it.type}:${it.id}`;
          return (
            <div key={key} className="p-4">
              <div className="flex items-start justify-between">
                <div className="min-w-0">
                  <div className="flex items-center gap-2 flex-wrap">
                    <span className="text-xs px-2 py-0.5 rounded bg-gray-100 text-gray-800">{it.type}</span>
                    <span className="font-medium">{it.type === 'Review' ? (it as UserReviewItem).reviewerName : it.reviewerName}</span>
                    <span className="text-gray-500">→</span>
                    <span className="font-medium">
                      {it.type === 'Review'
                        ? (it as UserReviewItem).revieweeName
                        : (it as EventReviewItem).eventName}
                    </span>
                    <ReasonBadge
                      level={it.moderationLevel}
                      source={it.moderationSource}
                      reason={it.reason}
                      reasonRu={it.reasonRu}
                      reasonEn={it.reasonEn}
                      className="ml-2"
                    />
                    <span className="text-xs text-gray-500">{new Date(it.createdAt).toLocaleString()}</span>
                  </div>

                  {(it as any).reportsCount > 0 && (
                    <span className="text-xs px-2 py-0.5 rounded bg-red-50 text-red-700">
                      Reports: {(it as any).reportsCount}
                    </span>
                  )}

                  {it.text && <p className="text-gray-800 mt-2">{it.text}</p>}

                  {it.type === 'Review' && ((it as UserReviewItem).leadRatings || (it as UserReviewItem).followRatings) && (
                    <div className="mt-2 grid grid-cols-2 gap-2 text-xs text-gray-700">
                      {(it as UserReviewItem).leadRatings && Object.entries((it as UserReviewItem).leadRatings!).map(([k, v]) => (
                        <div key={'L-' + k} className="flex justify-between bg-gray-50 rounded px-2 py-1">
                          <span className="capitalize">{t('aspects.lead')} {t(`aspects.${k}`) || k}</span><span className="font-semibold">{v}/5</span>
                        </div>
                      ))}
                      {(it as UserReviewItem).followRatings && Object.entries((it as UserReviewItem).followRatings!).map(([k, v]) => (
                        <div key={'F-' + k} className="flex justify-between bg-gray-50 rounded px-2 py-1">
                          <span className="capitalize">{t('aspects.follow')} {t(`aspects.${k}`) || k}</span><span className="font-semibold">{v}/5</span>
                        </div>
                      ))}
                    </div>
                  )}
                  {it.type === 'EventReview' && (it as EventReviewItem).ratings && (
                    <div className="mt-2 grid grid-cols-2 gap-2 text-xs text-gray-700">
                      {Object.entries((it as EventReviewItem).ratings!).map(([k, v]) => (
                        <div key={k} className="flex justify-between bg-gray-50 rounded px-2 py-1">
                          <span className="capitalize">{t(`aspects.${k}`) || k}</span><span className="font-semibold">{v}/5</span>
                        </div>
                      ))}
                    </div>
                  )}

                  {expanded[key] && (
                    <div className="mt-3 p-3 border border-gray-200 rounded bg-gray-50">
                      <ReportsBlock type={it.type} id={it.id} loadReports={loadReports} reports={reports[key]} />
                      <EditReason item={it} onApply={async (newLevel, reason) => await setLevel(it, newLevel, reason)} busy={busy} />
                    </div>
                  )}
                </div>

                <div className="flex flex-col gap-2 items-end">
                  <div className="flex gap-2">
                    <button className="btn-secondary" disabled={busy} onClick={() => setLevel(it, 'Green')}>{t('adminModeration.setGreen')}</button>
                    <button className="btn-secondary" disabled={busy} onClick={() => setLevel(it, 'Yellow')}>{t('adminModeration.setYellow')}</button>
                    <button className="btn-secondary" disabled={busy} onClick={() => setLevel(it, 'Red')}>{t('adminModeration.setRed')}</button>
                    <button className="btn-primary" disabled={busy} onClick={() => requeue(it)}>{t('adminModeration.requeue')}</button>
                  </div>
                  <button
                    className="text-sm text-gray-600 hover:underline"
                    onClick={async () => { toggle(key); await loadReports(it.type, it.id); }}
                  >
                    {expanded[key] ? t('adminModeration.hideDetails') : t('adminModeration.showDetails')}
                  </button>
                </div>
              </div>
            </div>
          );
        })}
        {filtered.length === 0 && <div className="p-4 text-gray-500">{t('adminModeration.noItems')}</div>}
      </div>
      <div className="mt-6 bg-white rounded shadow">
        <div className="px-4 py-3 border-b border-gray-200 flex items-center justify-between">
          <h2 className="text-lg font-semibold text-gray-900">{t('adminModeration.photoReports')}</h2>
          <button className="btn-secondary" onClick={loadPhotoReports} disabled={photosLoading || busy}>
            {photosLoading ? t('common.loading') : t('adminModeration.refresh')}
          </button>
        </div>
        {photosLoading ? (
          <div className="p-4 text-gray-500">{t('common.loading')}</div>
        ) : photoReports.length === 0 ? (
          <div className="p-4 text-gray-500">{t('adminModeration.photoReportsEmpty')}</div>
        ) : (
          <div className="divide-y">
            {photoReports.map(pr => (
              <div key={pr.id} className="p-4 flex gap-4 items-start">
                <div className="w-32 h-24 bg-gray-50 border rounded overflow-hidden flex items-center justify-center">
                  {pr.preview ? (
                    <img src={pr.preview.smallUrl} alt="" className="object-contain max-w-full max-h-full" />
                  ) : (
                    <span className="text-xs text-gray-400">{t('adminModeration.noPreview')}</span>
                  )}
                </div>
                <div className="flex-1 min-w-0">
                  <div className="text-sm text-gray-800">
                    <span className="text-gray-500">{t('adminModeration.by')}</span> {pr.reporterName}
                    <span className="text-gray-400 mx-2">•</span>
                    <span className="text-gray-500">{t('adminModeration.reason')}</span> {pr.reason}
                  </div>
                  <div className="text-xs text-gray-500">{new Date(pr.createdAt).toLocaleString()}</div>
                </div>
                <div className="flex gap-2">
                  <button className="btn-secondary" disabled={busy} onClick={() => resolvePhotoReport(pr.id, false)}>{t('adminModeration.keepResolve')}</button>
                  <button className="btn-secondary" disabled={busy} onClick={() => resolvePhotoReport(pr.id, true)}>{t('adminModeration.deleteResolve')}</button>
                </div>
              </div>
            ))}
          </div>
        )}
      </div>
    </div>
  );
};

const ReportsBlock: React.FC<{
  type: 'Review' | 'EventReview',
  id: number,
  reports?: any[],
  loadReports: (t: 'Review' | 'EventReview', id: number) => Promise<void>
}> = ({ type, id, reports, loadReports }) => {
  const { t } = useTranslation();
  useEffect(() => { if (!reports) loadReports(type, id); /* eslint-disable-next-line */ }, [type, id]);
  return (
    <div className="mb-3">
      <div className="text-sm font-semibold text-gray-800 mb-1">{t('adminModeration.reports')}</div>
      {!reports ? (
        <div className="text-gray-500 text-sm">{t('adminModeration.reportsLoading')}</div>
      ) : reports.length === 0 ? (
        <div className="text-gray-500 text-sm">{t('adminModeration.reportsEmpty')}</div>
      ) : (
        <div className="space-y-1">
          {reports.map((r: any) => (
            <div key={r.id} className="text-xs bg-white border border-gray-200 rounded px-2 py-1">
              <div className="text-gray-800"><span className="text-gray-500">{t('adminModeration.by')}</span> {r.reporterName}</div>
              <div><span className="text-gray-500">{t('adminModeration.reason')}</span> {r.reason}</div>
              {r.description && <div className="text-gray-600">{r.description}</div>}
              <div className="text-gray-500">{new Date(r.createdAt).toLocaleString()} • {r.status}</div>
            </div>
          ))}
        </div>
      )}
    </div>
  );
};

const EditReason: React.FC<{
  item: AnyItem;
  onApply: (level: ActionLevel, reason?: string) => Promise<void>;
  busy: boolean;
}> = ({ item, onApply, busy }) => {
  const { t } = useTranslation();
  const asAction = (lvl: Level): ActionLevel => (lvl === 'Pending' ? 'Yellow' : (lvl as ActionLevel));
  const [lvl, setLvl] = useState<ActionLevel>(asAction(item.moderationLevel));
  const browserLang = (navigator?.language || 'en').toLowerCase();
  const initial = item.reason ?? (browserLang.startsWith('ru') ? item.reasonRu : item.reasonEn) ?? '';
  const [rsn, setRsn] = useState<string>(initial);
  useEffect(() => {
    const init = item.reason ?? (browserLang.startsWith('ru') ? item.reasonRu : item.reasonEn) ?? '';
    setLvl(asAction(item.moderationLevel));
    setRsn(init);
    // eslint-disable-next-line
  }, [item]);

  return (
    <div className="mt-2">
      <div className="text-sm font-semibold text-gray-800 mb-1">{t('adminModeration.setLevelReason')}</div>
      <div className="flex flex-col md:flex-row gap-2 md:items-center">
        <select className="input-field flex-1" value={lvl} onChange={(e) => setLvl(e.target.value as ActionLevel)}>
          {(['Green', 'Yellow', 'Red'] as ActionLevel[]).map(x => <option key={x} value={x}>{x}</option>)}
        </select>
        <input
          className="input-field flex-1"
          placeholder={t('adminModeration.reason') || 'Reason'}
          value={rsn}
          onChange={(e) => setRsn(e.target.value)}
        />
        <button className="btn-primary" disabled={busy} onClick={() => onApply(lvl, rsn)}>{t('adminModeration.apply')}</button>
      </div>
    </div>
  );
};

export default AdminModeration;