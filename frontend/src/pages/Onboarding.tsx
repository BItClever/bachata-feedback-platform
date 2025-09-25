import React, { useEffect, useState } from 'react';
import { useAuth } from '../contexts/AuthContext';
import { usersAPI, userSettingsAPI, authAPI } from '../services/api';
import { useNavigate } from 'react-router-dom';
import { useTranslation } from 'react-i18next';

const Onboarding: React.FC = () => {
    const { user, updateUserData } = useAuth();
    const [step, setStep] = useState<1 | 2 | 3>(1);
    const [saving, setSaving] = useState(false);
    const [error, setError] = useState('');
    const navigate = useNavigate();
    const { t } = useTranslation();

    useEffect(() => { if (!user) navigate('/login'); }, [user, navigate]);

    const [form, setForm] = useState({
        firstName: user?.firstName || '',
        lastName: user?.lastName || '',
        nickname: user?.nickname || '',
        dancerRole: user?.dancerRole || '',
        selfAssessedLevel: user?.selfAssessedLevel || '',
    });

    const [settings, setSettings] = useState({
        allowReviews: true,
        showRatingsToOthers: true,
        showTextReviewsToOthers: true,
        allowAnonymousReviews: true,
        showPhotosToGuests: true,
    });

    useEffect(() => {
        (async () => {
            try {
                const s = await userSettingsAPI.getMine();
                setSettings(s.data);
            } catch { }
        })();
    }, []);

    const saveStep1 = async () => {
        if (!user) return;
        setSaving(true); setError('');
        try {
            await usersAPI.updateUser(user.id, form);
            const me = await authAPI.getCurrentUser();
            updateUserData(me.data);
            setStep(2);
        } catch (e: any) {
            setError(e?.response?.data?.message || t('errors.failedSaveProfile') || 'Failed to save profile');
        } finally {
            setSaving(false);
        }
    };

    const saveStep2 = async () => {
        setSaving(true); setError('');
        try {
            await userSettingsAPI.updateMine(settings);
            setStep(3);
        } catch (e: any) {
            setError(e?.response?.data?.message || t('errors.failedSaveSettings') || 'Failed to save settings');
        } finally {
            setSaving(false);
        }
    };

    if (!user) return null;

    return (
        <div className="max-w-2xl mx-auto px-4 py-8">
            <div className="bg-white rounded shadow p-6">
                <h1 className="text-2xl font-bold mb-4">{t('onboarding.title')}</h1>
                {error && <div className="mb-4 bg-red-50 border border-red-200 text-red-800 px-4 py-2 rounded">{error}</div>}

                {step === 1 && (
                    <div className="space-y-4">
                        <div className="text-gray-700">{t('onboarding.step1.intro')}</div>
                        <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
                            <div>
                                <label className="block text-sm font-medium text-gray-700 mb-1">{t('onboarding.step1.firstName')}</label>
                                <input className="input-field" value={form.firstName} onChange={e => setForm({ ...form, firstName: e.target.value })} />
                            </div>
                            <div>
                                <label className="block text-sm font-medium text-gray-700 mb-1">{t('onboarding.step1.lastName')}</label>
                                <input className="input-field" value={form.lastName} onChange={e => setForm({ ...form, lastName: e.target.value })} />
                            </div>
                            <div>
                                <label className="block text-sm font-medium text-gray-700 mb-1">{t('onboarding.step1.nickname')}</label>
                                <input className="input-field" value={form.nickname} onChange={e => setForm({ ...form, nickname: e.target.value })} />
                            </div>
                            <div>
                                <label className="block text-sm font-medium text-gray-700 mb-1">{t('onboarding.step1.role')}</label>
                                <select className="input-field" value={form.dancerRole} onChange={e => setForm({ ...form, dancerRole: e.target.value })}>
                                    <option value="">{t('onboarding.step1.selectRole')}</option>
                                    <option value="Lead">Lead</option>
                                    <option value="Follow">Follow</option>
                                    <option value="Both">Both</option>
                                </select>
                            </div>
                            <div>
                                <label className="block text-sm font-medium text-gray-700 mb-1">{t('onboarding.step1.level')}</label>
                                <select className="input-field" value={form.selfAssessedLevel} onChange={e => setForm({ ...form, selfAssessedLevel: e.target.value })}>
                                    <option value="">{t('onboarding.step1.selectLevel')}</option>
                                    {['Beginner', 'Beginner-Intermediate', 'Intermediate', 'Intermediate-Advanced', 'Advanced', 'Professional'].map(l => <option key={l} value={l}>{l}</option>)}
                                </select>
                            </div>
                        </div>
                        <div className="flex gap-3">
                            <button className="btn-secondary" onClick={() => navigate('/dashboard')}>{t('onboarding.step1.skip')}</button>
                            <button className="btn-primary" onClick={saveStep1} disabled={saving}>{saving ? t('onboarding.step1.saving') : t('onboarding.step1.saveContinue')}</button>
                        </div>
                    </div>
                )}

                {step === 2 && (
                    <div className="space-y-4">
                        <div className="text-gray-700">{t('onboarding.step2.intro')}</div>
                        {([
                            { key: 'allowReviews', label: t('profile.privacy.allowReviews') },
                            { key: 'showRatingsToOthers', label: t('profile.privacy.showRatingsToOthers') },
                            { key: 'showTextReviewsToOthers', label: t('profile.privacy.showTextReviewsToOthers') },
                            { key: 'allowAnonymousReviews', label: t('profile.privacy.allowAnonymousReviews') },
                            { key: 'showPhotosToGuests', label: t('profile.privacy.showPhotosToGuests') },
                        ] as const).map(i => (
                            <label key={i.key} className="flex items-center gap-2">
                                <input type="checkbox" checked={(settings as any)[i.key]} onChange={e => setSettings({ ...settings, [i.key]: e.target.checked })} />
                                <span>{i.label}</span>
                            </label>
                        ))}
                        <div className="flex gap-3">
                            <button className="btn-secondary" onClick={() => setStep(1)}>{t('onboarding.step2.back')}</button>
                            <button className="btn-primary" onClick={saveStep2} disabled={saving}>{saving ? t('onboarding.step2.saving') : t('onboarding.step2.saveContinue')}</button>
                        </div>
                    </div>
                )}

                {step === 3 && (
                    <div className="space-y-4">
                        <div className="text-gray-700">
                            {t('onboarding.step3.tipsIntro')}
                        </div>
                        <ul className="list-disc pl-6 text-gray-700">
                            <li>{t('onboarding.step3.bullets.numeric')}</li>
                            <li>{t('onboarding.step3.bullets.text')}</li>
                            <li>{t('onboarding.step3.bullets.control')}</li>
                        </ul>
                        <div>
                            <button className="btn-primary" onClick={() => navigate('/dashboard')}>{t('onboarding.step3.finish')}</button>
                        </div>
                    </div>
                )}
            </div>
        </div>
    );
};

export default Onboarding;