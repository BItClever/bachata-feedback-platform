import React, { useState, useEffect } from 'react';
import { useAuth } from '../contexts/AuthContext';
import { usersAPI } from '../services/api';

const Profile: React.FC = () => {
  const { user, updateUserData } = useAuth(); // Изменили на updateUserData
  const [isEditing, setIsEditing] = useState(false);
  const [formData, setFormData] = useState({
    firstName: user?.firstName || '',
    lastName: user?.lastName || '',
    nickname: user?.nickname || '',
    bio: user?.bio || '',
    selfAssessedLevel: user?.selfAssessedLevel || '',
    startDancingDate: user?.startDancingDate ? user.startDancingDate.split('T')[0] : '',
    danceStyles: user?.danceStyles || ''
  });
  const [isLoading, setIsLoading] = useState(false);
  const [error, setError] = useState('');
  const [success, setSuccess] = useState('');

  // Обновляем formData когда user изменяется
  useEffect(() => {
    if (user) {
      setFormData({
        firstName: user.firstName || '',
        lastName: user.lastName || '',
        nickname: user.nickname || '',
        bio: user.bio || '',
        selfAssessedLevel: user.selfAssessedLevel || '',
        startDancingDate: user.startDancingDate ? user.startDancingDate.split('T')[0] : '',
        danceStyles: user.danceStyles || ''
      });
    }
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

      const response = await usersAPI.updateUser(user.id.toString(), updateData);
      updateUserData(response.data); // Используем updateUserData
      setIsEditing(false);
      setSuccess('Profile updated successfully!');
    } catch (err: any) {
      setError(err.response?.data?.message || 'Failed to update profile');
    } finally {
      setIsLoading(false);
    }
  };

  // Остальной код остается без изменений...

  const levelOptions = [
    'Beginner',
    'Beginner-Intermediate',
    'Intermediate',
    'Intermediate-Advanced',
    'Advanced',
    'Professional'
  ];

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
            <div className="flex items-center space-x-6">
              <div className="w-24 h-24 bg-primary-100 rounded-full flex items-center justify-center">
                <span className="text-primary-600 font-bold text-2xl">
                  {user?.firstName[0]}{user?.lastName[0]}
                </span>
              </div>
              <div>
                <h2 className="text-2xl font-bold text-gray-900">
                  {user?.firstName} {user?.lastName}
                </h2>
                {user?.nickname && (
                  <p className="text-gray-600 text-lg">"{user.nickname}"</p>
                )}
                <p className="text-gray-600">{user?.email}</p>
              </div>
            </div>

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
          </div>
        )}
      </div>
    </div>
  );
};

export default Profile;