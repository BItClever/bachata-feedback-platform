import React, { useState, useEffect } from 'react';
import { useAuth } from '../contexts/AuthContext';
import { usersAPI, userSettingsAPI, reviewsAPI, Review } from '../services/api';
import { userPhotosAPI } from '../services/api';

const Profile: React.FC = () => {
  const { user, updateUserData } = useAuth();
  const [isEditing, setIsEditing] = useState(false);
  const [formData, setFormData] = useState({
    firstName: user?.firstName || '',
    lastName: user?.lastName || '',
    nickname: user?.nickname || '',
    bio: user?.bio || '',
    selfAssessedLevel: user?.selfAssessedLevel || '',
    startDancingDate: user?.startDancingDate ? user.startDancingDate.split('T')[0] : '',
    danceStyles: user?.danceStyles || '',
    dancerRole: user?.dancerRole || ''
  });
  const [isLoading, setIsLoading] = useState(false);
  const [error, setError] = useState('');
  const [success, setSuccess] = useState('');
  const [myReviews, setMyReviews] = useState<Review[]>([]);
  const [ratingsAgg, setRatingsAgg] = useState<{ lead: Record<string, number>, follow: Record<string, number> }>({
    lead: {},
    follow: {}
  });
  // Photos
  const [myPhotos, setMyPhotos] = useState<Array<{ id: number; isMain: boolean; smallUrl: string; mediumUrl: string; largeUrl: string }>>([]);
  const [uploading, setUploading] = useState(false);

  // Обновляем formData и загружаем отзывы/фото когда user изменяется
  useEffect(() => {
    const load = async () => {
      if (!user) return;
      try {
        const res = await reviewsAPI.getUserReviews(user.id);
        setMyReviews(res.data);
        const leadAcc: Record<string, { sum: number; count: number }> = {};
        const followAcc: Record<string, { sum: number; count: number }> = {};

        for (const r of res.data) {
          if (r.leadRatings) {
            for (const [k, v] of Object.entries(r.leadRatings)) {
              if (!leadAcc[k]) leadAcc[k] = { sum: 0, count: 0 };
              leadAcc[k].sum += v;
              leadAcc[k].count += 1;
            }
          }
          if (r.followRatings) {
            for (const [k, v] of Object.entries(r.followRatings)) {
              if (!followAcc[k]) followAcc[k] = { sum: 0, count: 0 };
              followAcc[k].sum += v;
              followAcc[k].count += 1;
            }
          }
        }

        const leadOut: Record<string, number> = {};
        for (const [k, { sum, count }] of Object.entries(leadAcc)) {
          leadOut[k] = count ? sum / count : 0;
        }

        const followOut: Record<string, number> = {};
        for (const [k, { sum, count }] of Object.entries(followAcc)) {
          followOut[k] = count ? sum / count : 0;
        }

        setRatingsAgg({ lead: leadOut, follow: followOut });

        // Photos
        const ph = await userPhotosAPI.getMine();
        setMyPhotos(ph.data);
      } catch (e) {
        // тихо игнорируем в MVP
      }
    };
    load();
  }, [user]);

  const handleChange = (e: React.ChangeEvent<HTMLInputElement | HTMLTextAreaElement | HTMLSelectElement>) => {
    setFormData({
      ...formData,
      [e.target.name]: e.target.value
    });
  };

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setIsLoading(true);
    setError('');
    setSuccess('');
    try {
      if (!user) return;

      const updateData = {
        ...formData,
        startDancingDate: formData.startDancingDate ? new Date(formData.startDancingDate).toISOString() : undefined
      };

      await usersAPI.updateUser(user.id.toString(), updateData);

      // Сразу забираем актуальный профиль с сервера
      const me = await usersAPI.getCurrentUser();
      updateUserData(me.data);

      setFormData({
        firstName: me.data.firstName || '',
        lastName: me.data.lastName || '',
        nickname: me.data.nickname || '',
        bio: me.data.bio || '',
        selfAssessedLevel: me.data.selfAssessedLevel || '',
        startDancingDate: me.data.startDancingDate ? me.data.startDancingDate.split('T')[0] : '',
        danceStyles: me.data.danceStyles || '',
        dancerRole: me.data.dancerRole || ''
      });

      setIsEditing(false);
      setSuccess('Profile updated successfully!');
    } catch (err: any) {
      setError(err.response?.data?.message || 'Failed to update profile');
    } finally {
      setIsLoading(false);
    }
  };

  const levelOptions = [
    'Beginner',
    'Beginner-Intermediate',
    'Intermediate',
    'Intermediate-Advanced',
    'Advanced',
    'Professional'
  ];

  const [settings, setSettings] = useState({
    allowReviews: true,
    showRatingsToOthers: true,
    showTextReviewsToOthers: true,
    allowAnonymousReviews: true,
    showPhotosToGuests: true,
  });
  const [savingSettings, setSavingSettings] = useState(false);

  useEffect(() => {
    const load = async () => {
      try {
        const resp = await userSettingsAPI.getMine();
        setSettings(resp.data);
      } catch { }
    };
    load();
  }, []);

  const saveSettings = async () => {
    setSavingSettings(true);
    try {
      await userSettingsAPI.updateMine(settings);
      setSuccess('Settings updated!');
    } catch (e: any) {
      setError(e.response?.data?.message || 'Failed to update settings');
    } finally {
      setSavingSettings(false);
    }
  };

  // Photos: actions
  const refreshPhotos = async () => {
    const ph = await userPhotosAPI.getMine();
    setMyPhotos(ph.data);
  };

  const onUpload = async (e: React.ChangeEvent<HTMLInputElement>) => {
    if (!e.target.files || e.target.files.length === 0) return;
    const file = e.target.files[0];
    setUploading(true);
    setError('');
    setSuccess('');
    try {
      await userPhotosAPI.uploadMyPhoto(file);
      await refreshPhotos();
      setSuccess('Photo uploaded');
    } catch (err: any) {
      setError(err.response?.data?.message || 'Failed to upload photo');
    } finally {
      setUploading(false);
      e.target.value = '';
    }
  };

  const setMainPhoto = async (photoId: number) => {
    try {
      await userPhotosAPI.setMain(photoId);
      await refreshPhotos();
      // обновим профиль (MainPhotoPath)
      const me = await usersAPI.getCurrentUser();
      updateUserData(me.data);
    } catch (err: any) {
      setError(err.response?.data?.message || 'Failed to set main photo');
    }
  };

  const deletePhoto = async (photoId: number) => {
    try {
      await userPhotosAPI.delete(photoId);
      await refreshPhotos();
      setSuccess('Photo deleted');
    } catch (err: any) {
      setError(err.response?.data?.message || 'Failed to delete photo');
    }
  };

  const mainPhoto = myPhotos.find(p => p.isMain);

  return (
    <div className="max-w-4xl mx-auto px-4 py-8">
      <div className="bg-white rounded-lg shadow-lg p-8">
        <div className="flex justify-between items-center mb-8">
          <h1 className="text-3xl font-bold text-gray-900">My Profile</h1>
          {!isEditing && (
            <button
              onClick={() => setIsEditing(true)}
              className="btn-primary"
            >
              Edit Profile
            </button>
          )}
        </div>
        {success && (
          <div className="bg-green-50 border border-green-400 text-green-700 px-4 py-3 rounded mb-6">
            {success}
          </div>
        )}

        {error && (
          <div className="bg-red-50 border border-red-400 text-red-700 px-4 py-3 rounded mb-6">
            {error}
          </div>
        )}

        {/* Header with avatar */}
        <div className="flex items-center space-x-6 mb-6">
          <div className="w-24 h-24 bg-primary-100 rounded-full flex items-center justify-center overflow-hidden">
            {mainPhoto ? (
              <img src={mainPhoto.smallUrl} alt="avatar" className="w-24 h-24 object-cover" />
            ) : (
              <span className="text-primary-600 font-bold text-2xl">
                {user?.firstName?.[0]}{user?.lastName?.[0]}
              </span>
            )}
          </div>
          <div>
            <h2 className="text-2xl font-bold text-gray-900">
              {user?.firstName} {user?.lastName}
            </h2>
            {user?.nickname && (
              <p className="text-gray-600 text-lg">"{user.nickname}"</p>
            )}
            <p className="text-gray-600">{user?.email}</p>
            <div className="mt-3">
              <label className="btn-secondary relative cursor-pointer">
                <input type="file" accept="image/jpeg,image/png,image/webp" onChange={onUpload} className="absolute inset-0 opacity-0 cursor-pointer" />
                {uploading ? 'Uploading...' : 'Upload Avatar/Photo'}
              </label>
            </div>
          </div>
        </div>

        {isEditing ? (
          <form onSubmit={handleSubmit} className="space-y-6">
            <div className="grid grid-cols-1 md:grid-cols-2 gap-6">
              <div>
                <label className="block text-sm font-medium text-gray-700 mb-2">
                  First Name
                </label>
                <input
                  type="text"
                  name="firstName"
                  required
                  className="input-field"
                  value={formData.firstName}
                  onChange={handleChange}
                />
              </div>
              <div>
                <label className="block text-sm font-medium text-gray-700 mb-2">
                  Last Name
                </label>
                <input
                  type="text"
                  name="lastName"
                  required
                  className="input-field"
                  value={formData.lastName}
                  onChange={handleChange}
                />
              </div>
            </div>

            <div>
              <label className="block text-sm font-medium text-gray-700 mb-2">
                Nickname
              </label>
              <input
                type="text"
                name="nickname"
                className="input-field"
                value={formData.nickname}
                onChange={handleChange}
              />
            </div>

            <div>
              <label className="block text-sm font-medium text-gray-700 mb-2">
                Bio
              </label>
              <textarea
                name="bio"
                className="input-field h-24"
                value={formData.bio}
                onChange={handleChange}
                placeholder="Tell us about yourself and your dancing journey..."
              />
            </div>

            <div className="grid grid-cols-1 md:grid-cols-2 gap-6">
              <div>
                <label className="block text-sm font-medium text-gray-700 mb-2">
                  Self-Assessed Level
                </label>
                <select
                  name="selfAssessedLevel"
                  className="input-field"
                  value={formData.selfAssessedLevel}
                  onChange={handleChange}
                >
                  <option value="">Select your level</option>
                  {levelOptions.map(level => (
                    <option key={level} value={level}>{level}</option>
                  ))}
                </select>
              </div>
              <div>
                <label className="block text-sm font-medium text-gray-700 mb-2">
                  Dancer Role
                </label>
                <select
                  name="dancerRole"
                  className="input-field"
                  value={formData.dancerRole}
                  onChange={handleChange}
                >
                  <option value="">Select role</option>
                  <option value="Lead">Lead</option>
                  <option value="Follow">Follow</option>
                  <option value="Both">Both</option>
                </select>
              </div>
              <div>
                <label className="block text-sm font-medium text-gray-700 mb-2">
                  Started Dancing
                </label>
                <input
                  type="date"
                  name="startDancingDate"
                  className="input-field"
                  value={formData.startDancingDate}
                  onChange={handleChange}
                />
              </div>
            </div>

            <div>
              <label className="block text-sm font-medium text-gray-700 mb-2">
                Dance Styles
              </label>
              <input
                type="text"
                name="danceStyles"
                className="input-field"
                value={formData.danceStyles}
                onChange={handleChange}
                placeholder="e.g., Bachata Sensual, Traditional, Dominican"
              />
            </div>

            <div className="flex space-x-4">
              <button
                type="button"
                onClick={() => setIsEditing(false)}
                className="btn-secondary flex-1"
              >
                Cancel
              </button>
              <button
                type="submit"
                disabled={isLoading}
                className="btn-primary flex-1"
              >
                {isLoading ? 'Saving...' : 'Save Changes'}
              </button>
            </div>
          </form>
        ) : (
          <div className="space-y-6">
            {user?.bio && (
              <div>
                <h3 className="text-lg font-medium text-gray-900 mb-2">About</h3>
                <p className="text-gray-700">{user.bio}</p>
              </div>
            )}

            <div className="grid grid-cols-1 md:grid-cols-2 gap-6">
              {user?.selfAssessedLevel && (
                <div>
                  <h3 className="text-lg font-medium text-gray-900 mb-2">Level</h3>
                  <p className="text-gray-700">{user.selfAssessedLevel}</p>
                </div>
              )}
              {user?.dancerRole && (
                <div>
                  <h3 className="text-lg font-medium text-gray-900 mb-2">Role</h3>
                  <p className="text-gray-700">{user.dancerRole}</p>
                </div>
              )}
              {user?.startDancingDate && (
                <div>
                  <h3 className="text-lg font-medium text-gray-900 mb-2">Dancing Since</h3>
                  <p className="text-gray-700">
                    {new Date(user.startDancingDate).toLocaleDateString()}
                  </p>
                </div>
              )}
            </div>

            {user?.danceStyles && (
              <div>
                <h3 className="text-lg font-medium text-gray-900 mb-2">Dance Styles</h3>
                <p className="text-gray-700">{user.danceStyles}</p>
              </div>
            )}

            {(Object.keys(ratingsAgg.lead).length > 0 || Object.keys(ratingsAgg.follow).length > 0) && (
              <div>
                <h3 className="text-lg font-medium text-gray-900 mb-2">My Ratings</h3>

                {Object.keys(ratingsAgg.lead).length > 0 && (
                  <div className="mb-4">
                    <h4 className="text-sm font-semibold text-gray-700 mb-2">Lead</h4>
                    <div className="grid grid-cols-1 md:grid-cols-2 gap-2">
                      {Object.entries(ratingsAgg.lead).map(([k, v]) => (
                        <div key={k} className="flex justify-between bg-gray-50 rounded px-3 py-2">
                          <span className="capitalize text-gray-700">{k}</span>
                          <span className="font-semibold text-gray-900">{v.toFixed(1)}/5</span>
                        </div>
                      ))}
                    </div>
                  </div>
                )}

                {Object.keys(ratingsAgg.follow).length > 0 && (
                  <div className="mb-2">
                    <h4 className="text-sm font-semibold text-gray-700 mb-2">Follow</h4>
                    <div className="grid grid-cols-1 md:grid-cols-2 gap-2">
                      {Object.entries(ratingsAgg.follow).map(([k, v]) => (
                        <div key={k} className="flex justify-between bg-gray-50 rounded px-3 py-2">
                          <span className="capitalize text-gray-700">{k}</span>
                          <span className="font-semibold text-gray-900">{v.toFixed(1)}/5</span>
                        </div>
                      ))}
                    </div>
                  </div>
                )}
              </div>
            )}

            {/* Мои фото */}
            <div>
              <h3 className="text-lg font-medium text-gray-900 mb-2">My Photos</h3>
              {myPhotos.length === 0 ? (
                <p className="text-gray-500">No photos yet. Upload your first photo!</p>
              ) : (
                <div className="grid grid-cols-2 md:grid-cols-4 gap-4">
                  {myPhotos.map(p => (
                    <div key={p.id} className="border rounded-lg overflow-hidden">
                      <img src={p.mediumUrl} alt="" className="w-full h-40 object-cover" />
                      <div className="p-2 flex items-center justify-between text-sm">
                        {p.isMain ? <span className="text-green-700 font-semibold">Main</span> : (
                          <button className="text-primary-600 hover:underline" onClick={() => setMainPhoto(p.id)}>Set main</button>
                        )}
                        <button className="text-red-600 hover:underline" onClick={() => deletePhoto(p.id)}>Delete</button>
                      </div>
                    </div>
                  ))}
                </div>
              )}
            </div>
          </div>
        )}
      </div>

      <div className="bg-white rounded-lg shadow-lg p-8 mt-8">
        <h2 className="text-2xl font-bold text-gray-900 mb-4">Privacy Settings</h2>
        <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
          {[
            { key: 'allowReviews', label: 'Allow reviews' },
            { key: 'showRatingsToOthers', label: 'Show numeric ratings to others' },
            { key: 'showTextReviewsToOthers', label: 'Show text reviews to others' },
            { key: 'allowAnonymousReviews', label: 'Allow anonymous reviews' },
            { key: 'showPhotosToGuests', label: 'Show photos to guests' },
          ].map(item => (
            <label key={item.key} className="flex items-center space-x-2">
              <input
                type="checkbox"
                checked={(settings as any)[item.key]}
                onChange={(e) => setSettings(prev => ({ ...prev, [item.key]: e.target.checked }))}
              />
              <span>{item.label}</span>
            </label>
          ))}
        </div>

        <div className="mt-4">
          <button onClick={saveSettings} disabled={savingSettings} className="btn-primary">
            {savingSettings ? 'Saving...' : 'Save Settings'}
          </button>
        </div>
      </div>
    </div>
  );
};

export default Profile;