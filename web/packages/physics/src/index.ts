export * from '@axrone/physics-core';
export * from '@axrone/physics-2d';
export * from '@axrone/physics-3d';
// Raycast alt sistemi @axrone/raycast paketine taşındı (refactor plan 2.2);
// şemsiye facade tüm raycast yüzeyini aynen yeniden dışa aktarır.
export * from '@axrone/raycast';

// TS2308 belirsizlik çözümü: şemsiye paket düzeyinde physics-core'un generic
// sabiti kazanır; 3D'ye özgü joint sabiti açık takma adla erişilebilir kalır.
export { INVALID_CONSTRAINT_ID } from '@axrone/physics-core';
export { INVALID_CONSTRAINT_ID as INVALID_JOINT_CONSTRAINT_ID_3D } from '@axrone/physics-3d';

// Star export'lar çakışan isimleri sessizce düşürür (TS2308); raycast'in
// çekirdek runtime enum/tipleri physics-core ile paylaşıldığı için açıkça
// yeniden dışa aktarılır — eski facade yüzeyi birebir korunur.
export type {
    RaycastId,
    LayerMask,
    IRay2D,
    IRay3D,
    IRaycastHit2D,
    IRaycastHit3D,
    IRaycastQuery2D,
    IRaycastQuery3D,
    RaycastPredicate2D,
    RaycastPredicate3D,
    IBarycentricCoords,
} from '@axrone/raycast';
// Runtime enums must not go through `export type`, otherwise consumers get
// `undefined` at runtime (they are values, not just types).
export { RaycastFlags, RaycastLayer } from '@axrone/raycast';
