import React, { useEffect, useRef, useState } from 'react';

interface AvatarFocusModalProps {
  isOpen: boolean;
  onClose: () => void;
  photo: {
    id: number;
    smallUrl: string;
    mediumUrl: string;
    largeUrl: string;
    focusX?: number;
    focusY?: number;
  };
  onSave: (focusX: number, focusY: number) => Promise<void>;
}

const clamp = (n: number) => Math.max(0, Math.min(100, n));

const AvatarFocusModal: React.FC<AvatarFocusModalProps> = ({ isOpen, onClose, photo, onSave }) => {
  const [fx, setFx] = useState<number>(photo.focusX ?? 50);
  const [fy, setFy] = useState<number>(photo.focusY ?? 50);
  const [dragging, setDragging] = useState(false);
  const [busy, setBusy] = useState(false);
  const areaRef = useRef<HTMLDivElement | null>(null);

  useEffect(() => {
    setFx(photo.focusX ?? 50);
    setFy(photo.focusY ?? 50);
  }, [photo]);

  const setFromPointer = (clientX: number, clientY: number) => {
    const el = areaRef.current;
    if (!el) return;
    const rect = el.getBoundingClientRect();
    const x = ((clientX - rect.left) / rect.width) * 100;
    const y = ((clientY - rect.top) / rect.height) * 100;
    setFx(clamp(x));
    setFy(clamp(y));
  };

  const onPointerDown: React.PointerEventHandler<HTMLDivElement> = (e) => {
    (e.currentTarget as HTMLElement).setPointerCapture(e.pointerId);
    setDragging(true);
    setFromPointer(e.clientX, e.clientY);
  };

  const onPointerMove: React.PointerEventHandler<HTMLDivElement> = (e) => {
    if (!dragging) return;
    setFromPointer(e.clientX, e.clientY);
  };

  const onPointerUp: React.PointerEventHandler<HTMLDivElement> = (e) => {
    try { (e.currentTarget as HTMLElement).releasePointerCapture(e.pointerId); } catch {}
    setDragging(false);
  };

  if (!isOpen) return null;

  return (
    <div className="fixed inset-0 bg-black/40 backdrop-blur-sm z-50 flex items-center justify-center px-4">
      <div className="bg-white rounded-lg shadow-xl w-full max-w-lg">
        <div className="p-4 border-b border-gray-200 flex items-center justify-between">
          <h3 className="text-lg font-semibold text-gray-900">Настроить фокус аватара</h3>
          <button onClick={onClose} className="text-gray-500 hover:text-gray-700" aria-label="Close">✕</button>
        </div>

        <div className="p-4">
          <p className="text-sm text-gray-600 mb-3">
            Перетащи внутри круга туда, где должен быть центр внимания (лицо/ключевой объект).
          </p>

          <div
            ref={areaRef}
            className="relative mx-auto"
            style={{ width: 260, height: 260 }}
            onPointerDown={onPointerDown}
            onPointerMove={onPointerMove}
            onPointerUp={onPointerUp}
            onPointerCancel={onPointerUp}
          >
            <div className="absolute inset-0 rounded-full overflow-hidden border border-gray-200 shadow-inner">
              <img
                src={photo.largeUrl || photo.mediumUrl || photo.smallUrl}
                alt=""
                className="w-full h-full object-cover select-none"
                draggable={false}
                style={{ objectPosition: `${fx}% ${fy}%` }}
              />
            </div>
            {/* Кроссхэйр (индикатор точки фокуса) */}
            <div
              className="absolute w-4 h-4 -translate-x-1/2 -translate-y-1/2 rounded-full border-2 border-white shadow"
              style={{ left: `${fx}%`, top: `${fy}%` }}
            />
          </div>

          <div className="mt-4 grid grid-cols-2 gap-3">
            <div>
              <label className="block text-xs text-gray-600 mb-1">Горизонталь (X)</label>
              <input
                type="range"
                min={0}
                max={100}
                value={fx}
                onChange={(e) => setFx(Number(e.target.value))}
                className="w-full"
              />
            </div>
            <div>
              <label className="block text-xs text-gray-600 mb-1">Вертикаль (Y)</label>
              <input
                type="range"
                min={0}
                max={100}
                value={fy}
                onChange={(e) => setFy(Number(e.target.value))}
                className="w-full"
              />
            </div>
          </div>
        </div>

        <div className="p-4 border-t border-gray-200 flex gap-3 justify-end">
          <button className="btn-secondary" onClick={onClose} disabled={busy}>Отмена</button>
          <button
            className="btn-primary"
            onClick={async () => {
              setBusy(true);
              try {
                await onSave(fx, fy);
                onClose();
              } finally {
                setBusy(false);
              }
            }}
            disabled={busy}
          >
            {busy ? 'Сохранение…' : 'Сохранить'}
          </button>
        </div>
      </div>
    </div>
  );
};

export default AvatarFocusModal;