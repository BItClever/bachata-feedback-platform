import React, { useEffect, useState } from 'react';
import { useAuth } from '../contexts/AuthContext';
import { usersAPI, userSettingsAPI } from '../services/api';
import { useNavigate } from 'react-router-dom';

const Onboarding: React.FC = () => {
  const { user, updateUserData } = useAuth();
  const [step, setStep] = useState<1|2|3>(1);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState('');
  const navigate = useNavigate();

  useEffect(() => { if (!user) navigate('/login'); }, [user, navigate]);

  // Шаг 1: профиль
  const [form, setForm] = useState({
    firstName: user?.firstName || '',
    lastName: user?.lastName || '',
    nickname: user?.nickname || '',
    dancerRole: user?.dancerRole || '',
    selfAssessedLevel: user?.selfAssessedLevel || '',
  });

  // Шаг 2: приватность
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
      } catch {}
    })();
  }, []);

  const saveStep1 = async () => {
    if (!user) return;
    setSaving(true); setError('');
    try {
      await usersAPI.updateUser(user.id, form);
      const me = await usersAPI.getCurrentUser();
      updateUserData(me.data);
      setStep(2);
    } catch (e:any) {
      setError(e?.response?.data?.message || 'Failed to save profile');
    } finally {
      setSaving(false);
    }
  };

  const saveStep2 = async () => {
    setSaving(true); setError('');
    try {
      await userSettingsAPI.updateMine(settings);
      setStep(3);
    } catch (e:any) {
      setError(e?.response?.data?.message || 'Failed to save settings');
    } finally {
      setSaving(false);
    }
  };

  if (!user) return null;

  return (
    <div className="max-w-2xl mx-auto px-4 py-8">
      <div className="bg-white rounded shadow p-6">
        <h1 className="text-2xl font-bold mb-4">Welcome! Let’s set up your account</h1>
        {error && <div className="mb-4 bg-red-50 border border-red-200 text-red-800 px-4 py-2 rounded">{error}</div>}

        {step === 1 && (
          <div className="space-y-4">
            <div className="text-gray-700">Tell us a bit about you to improve review suggestions.</div>
            <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
              <div>
                <label className="block text-sm font-medium text-gray-700 mb-1">First name</label>
                <input className="input-field" value={form.firstName} onChange={e=>setForm({...form, firstName: e.target.value})}/>
              </div>
              <div>
                <label className="block text-sm font-medium text-gray-700 mb-1">Last name</label>
                <input className="input-field" value={form.lastName} onChange={e=>setForm({...form, lastName: e.target.value})}/>
              </div>
              <div>
                <label className="block text-sm font-medium text-gray-700 mb-1">Nickname (optional)</label>
                <input className="input-field" value={form.nickname} onChange={e=>setForm({...form, nickname: e.target.value})}/>
              </div>
              <div>
                <label className="block text-sm font-medium text-gray-700 mb-1">Role</label>
                <select className="input-field" value={form.dancerRole} onChange={e=>setForm({...form, dancerRole: e.target.value})}>
                  <option value="">Select role</option>
                  <option value="Lead">Lead</option>
                  <option value="Follow">Follow</option>
                  <option value="Both">Both</option>
                </select>
              </div>
              <div>
                <label className="block text-sm font-medium text-gray-700 mb-1">Level</label>
                <select className="input-field" value={form.selfAssessedLevel} onChange={e=>setForm({...form, selfAssessedLevel: e.target.value})}>
                  <option value="">Select your level</option>
                  {['Beginner','Beginner-Intermediate','Intermediate','Intermediate-Advanced','Advanced','Professional'].map(l=><option key={l} value={l}>{l}</option>)}
                </select>
              </div>
            </div>
            <div className="flex gap-3">
              <button className="btn-secondary" onClick={()=>navigate('/dashboard')}>Skip</button>
              <button className="btn-primary" onClick={saveStep1} disabled={saving}>{saving?'Saving...':'Save & continue'}</button>
            </div>
          </div>
        )}

        {step === 2 && (
          <div className="space-y-4">
            <div className="text-gray-700">Privacy controls. You can change them anytime in Profile.</div>
            {([
              {key:'allowReviews', label:'Allow reviews'},
              {key:'showRatingsToOthers', label:'Show numeric ratings to others'},
              {key:'showTextReviewsToOthers', label:'Show text reviews to others'},
              {key:'allowAnonymousReviews', label:'Allow anonymous reviews'},
              {key:'showPhotosToGuests', label:'Show photos to guests'},
            ] as const).map(i=>(
              <label key={i.key} className="flex items-center gap-2">
                <input type="checkbox" checked={(settings as any)[i.key]} onChange={e=>setSettings({...settings, [i.key]: e.target.checked})}/>
                <span>{i.label}</span>
              </label>
            ))}
            <div className="flex gap-3">
              <button className="btn-secondary" onClick={()=>setStep(1)}>Back</button>
              <button className="btn-primary" onClick={saveStep2} disabled={saving}>{saving?'Saving...':'Save & continue'}</button>
            </div>
          </div>
        )}

        {step === 3 && (
          <div className="space-y-4">
            <div className="text-gray-700">
              Tips: leave constructive, specific feedback. After events, you can rate the event and partners you danced with.
            </div>
            <ul className="list-disc pl-6 text-gray-700">
              <li>Numeric stars: quick, anonymous, shown faster.</li>
              <li>Text: helpful but goes through moderation.</li>
              <li>You control visibility in Settings.</li>
            </ul>
            <div>
              <button className="btn-primary" onClick={()=>navigate('/dashboard')}>Finish</button>
            </div>
          </div>
        )}
      </div>
    </div>
  );
};

export default Onboarding;