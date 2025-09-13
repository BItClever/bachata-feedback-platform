import React, { useEffect, useMemo, useState } from 'react';
import { adminRolesAPI, usersAPI, User } from '../services/api';
import { useAuth } from '../contexts/AuthContext';

type RoleInfo = { id: string; name: string; permissions: string[] };

const AdminRoles: React.FC = () => {
    const { user: me } = useAuth();
    const [loading, setLoading] = useState(true);
    const [error, setError] = useState('');
    const [roles, setRoles] = useState<RoleInfo[]>([]);
    const [allPermissions, setAllPermissions] = useState<string[]>([]);

    const [users, setUsers] = useState<User[]>([]);
    const [userSearch, setUserSearch] = useState('');
    const [selectedUser, setSelectedUser] = useState<User | null>(null);
    const [selectedUserInfo, setSelectedUserInfo] = useState<{ roles: string[]; permissions: string[] } | null>(null);
    const [busy, setBusy] = useState(false);
    const [toast, setToast] = useState<string>('');

    const canSee = me?.roles?.includes('Admin');

    const load = async () => {
        try {
            setLoading(true);
            setError('');
            const [r1, r2, r3] = await Promise.all([
                adminRolesAPI.getRoles(),
                adminRolesAPI.getAllPermissions(),
                usersAPI.getUsers()
            ]);
            setRoles(r1.data);
            setAllPermissions(r2.data);
            setUsers(r3.data);
        } catch (e: any) {
            setError(e?.response?.data?.message || 'Failed to load roles/permissions');
        } finally {
            setLoading(false);
        }
    };

    useEffect(() => { load(); }, []);

    const filteredUsers = useMemo(() => {
        const q = userSearch.trim().toLowerCase();
        if (!q) return users.slice(0, 20);
        return users.filter(u =>
            u.firstName.toLowerCase().includes(q) ||
            u.lastName.toLowerCase().includes(q) ||
            (u.nickname && u.nickname.toLowerCase().includes(q)) ||
            u.email.toLowerCase().includes(q)
        ).slice(0, 50);
    }, [users, userSearch]);

    const selectUser = async (u: User) => {
        setSelectedUser(u);
        setSelectedUserInfo(null);
        try {
            setBusy(true);
            const r = await adminRolesAPI.getUserRoles(u.id);
            setSelectedUserInfo({ roles: r.data.roles, permissions: r.data.permissions });
        } catch (e: any) {
            setError(e?.response?.data?.message || 'Failed to load user roles');
        } finally {
            setBusy(false);
        }
    };

    const syncRoles = async () => {
        try {
            setBusy(true);
            await adminRolesAPI.syncRoles();
            await load();
            showToast('Roles synchronized');
        } catch (e: any) {
            setError(e?.response?.data?.message || 'Failed to sync roles');
        } finally {
            setBusy(false);
        }
    };

    const assignRole = async (role: string) => {
        if (!selectedUser) return;
        try {
            setBusy(true);
            await adminRolesAPI.assignRole(selectedUser.id, role);
            await selectUser(selectedUser);
            showToast(`Role ${ role } assigned`);
        } catch (e: any) {
            setError(e?.response?.data?.message || 'Failed to assign role');
        } finally {
            setBusy(false);
        }
    };

    const revokeRole = async (role: string) => {
        if (!selectedUser) return;
        try {
            setBusy(true);
            await adminRolesAPI.revokeRole(selectedUser.id, role);
            await selectUser(selectedUser);
            showToast(`Role ${ role } revoked`);
        } catch (e: any) {
            setError(e?.response?.data?.message || 'Failed to revoke role');
        } finally {
            setBusy(false);
        }
    };

    const showToast = (msg: string) => {
        setToast(msg);
        setTimeout(() => setToast(''), 2500);
    };

    if (!canSee) {
        return (
            <div className="max-w-7xl mx-auto px-4 py-8">
                <h1 className="text-2xl font-bold text-gray-900">Admin Roles</h1>
                <p className="text-gray-600 mt-2">Access denied.</p>
            </div>
        );
    }

    if (loading) {
        return (
            <div className="flex justify-center items-center min-h-screen">
                <div className="animate-spin rounded-full h-32 w-32 border-b-2 border-primary-600"></div>
            </div>
        );
    }

    return (
        <div className="max-w-7xl mx-auto px-4 py-8">
            <div className="mb-6 flex items-center justify-between">
                <h1 className="text-2xl font-bold text-gray-900">Admin Roles</h1>
                <button className="btn-secondary" onClick={syncRoles} disabled={busy}>Sync Roles</button>
            </div>
            {toast && (
                <div className="mb-4 bg-green-50 border border-green-200 text-green-800 px-4 py-2 rounded">{toast}</div>
            )}

            {error && (
                <div className="mb-4 bg-red-50 border border-red-200 text-red-800 px-4 py-2 rounded">{error}</div>
            )}

            <div className="grid grid-cols-1 lg:grid-cols-2 gap-6">
                <div className="bg-white rounded-lg shadow p-6">
                    <h2 className="text-lg font-semibold text-gray-900 mb-3">Roles & Permissions</h2>
                    {roles.length === 0 ? (
                        <p className="text-gray-500">No roles found.</p>
                    ) : (
                        <div className="space-y-4">
                            {roles.map(r => (
                                <div key={r.id} className="border border-gray-200 rounded p-3">
                                    <div className="flex items-center justify-between">
                                        <div className="font-medium">{r.name}</div>
                                        <div className="text-xs text-gray-500">{r.permissions.length} perms</div>
                                    </div>
                                    {r.permissions.length > 0 ? (
                                        <div className="mt-2 flex flex-wrap gap-2">
                                            {r.permissions.map(p => (
                                                <span key={p} className="text-xs bg-gray-100 text-gray-800 rounded px-2 py-1">{p}</span>
                                            ))}
                                        </div>
                                    ) : (
                                        <p className="text-sm text-gray-500 mt-2">No permissions.</p>
                                    )}
                                </div>
                            ))}
                        </div>
                    )}
                    <div className="mt-4">
                        <h3 className="text-sm font-semibold text-gray-800 mb-1">All permissions</h3>
                        <div className="flex flex-wrap gap-2">
                            {allPermissions.map(p => (
                                <span key={p} className="text-xs bg-gray-50 border border-gray-200 text-gray-700 rounded px-2 py-1">{p}</span>
                            ))}
                        </div>
                    </div>
                </div>

                <div className="bg-white rounded-lg shadow p-6">
                    <h2 className="text-lg font-semibold text-gray-900 mb-3">Assign / Revoke Roles</h2>
                    <div className="mb-3">
                        <input
                            type="text"
                            className="input-field"
                            placeholder="Search users by name, nickname or email..."
                            value={userSearch}
                            onChange={(e) => setUserSearch(e.target.value)}
                        />
                    </div>
                    <div className="max-h-64 overflow-auto border border-gray-200 rounded mb-4">
                        {filteredUsers.map(u => (
                            <button
                                key={u.id}
                                onClick={() => selectUser(u)}
                                className={`w-full text-left px-3 py-2 hover:bg-gray-50 ${selectedUser?.id === u.id ? 'bg-gray-100' : ''}`}
                            >
                                <div className="flex items-center justify-between">
                                    <span className="text-sm text-gray-900">{u.firstName} {u.lastName}</span>
                                    <span className="text-xs text-gray-500">{u.email}</span>
                                </div>
                                {u.nickname && <div className="text-xs text-gray-500">"{u.nickname}"</div>}
                            </button>
                        ))}
                        {filteredUsers.length === 0 && (
                            <div className="px-3 py-2 text-sm text-gray-500">No users found</div>
                        )}
                    </div>

                    {!selectedUser ? (
                        <p className="text-gray-500">Select a user to manage roles.</p>
                    ) : (
                        <div>
                            <div className="mb-2 text-sm">
                                <span className="font-medium text-gray-800">Selected:</span>{' '}
                                {selectedUser.firstName} {selectedUser.lastName} ({selectedUser.email})
                            </div>

                            <div className="mb-3">
                                <div className="text-sm font-semibold text-gray-800 mb-1">Current roles</div>
                                {busy ? (
                                    <div className="text-gray-500">Loading...</div>
                                ) : selectedUserInfo ? (
                                    selectedUserInfo.roles.length > 0 ? (
                                        <div className="flex flex-wrap gap-2">
                                            {selectedUserInfo.roles.map(r => (
                                                <span key={r} className="text-xs bg-blue-50 text-blue-700 border border-blue-200 rounded px-2 py-1">{r}</span>
                                            ))}
                                        </div>
                                    ) : <div className="text-sm text-gray-500">No roles</div>
                                ) : null}
                            </div>

                            <div className="mb-3">
                                <div className="text-sm font-semibold text-gray-800 mb-1">Actions</div>
                                <div className="flex flex-wrap gap-2">
                                    {['Admin', 'Moderator', 'Organizer', 'User'].map(r => (
                                        <button key={r} onClick={() => assignRole(r)} disabled={busy} className="btn-secondary">{`Assign ${r}`}</button>
                                    ))}
                                </div>
                            </div>

                            <div>
                                <div className="text-sm font-semibold text-gray-800 mb-1">Revoke</div>
                                <div className="flex flex-wrap gap-2">
                                    {['Admin', 'Moderator', 'Organizer', 'User'].map(r => (
                                        <button key={r} onClick={() => revokeRole(r)} disabled={busy} className="btn-secondary">{`Revoke ${r}`}</button>
                                    ))}
                                </div>
                            </div>
                        </div>
                    )}
                </div>
            </div>
        </div>
    );
};

export default AdminRoles;