# @axrone/raycast

Ray sorgulama (raycast/shapecast/sweep) alt sistemi. `@axrone/physics`
şemsiye paketinden ayrıştırılmıştır; 2D/3D ray-primitive kesişimleri,
BVH/spatial-hash/octree hızlandırıcıları, raycast sistemleri ve
raycast'e özgü 3D broadphase yardımcıları burada yaşar.

## Bağımlılıklar

- `@axrone/numeric` — vektör/epsilon temelleri
- `@axrone/geometry` — AABB/Octree primitifleri
- `@axrone/hash` — sorgu önbelleği anahtarlama
- `@axrone/physics-core` — paylaşılan raycast tip sözleşmeleri
  (`IRay*`, `IRaycastHit*`, `RaycastFlags`, `RaycastLayer`) ve `ShapeType`

Simülasyon broadphase'i bu paketin kapsamı DEĞİLDİR: `physics-2d`
(`DynamicAABBTree2D`) ve `physics-3d` (`DynamicAABBTree3D`) kendi
simülasyon broadphase'lerine sahiptir. Buradaki `SpatialHashBroadphase3D`
ve `OctreeBroadphase3D` yalnızca ray sorguları için hızlandırıcıdır.
2D motor broadphase entegrasyonu `IRaycastBroadphaseSource2D` yapısal
arayüzü üzerinden sağlanır (pakete `physics-2d` bağımlılığı eklemeden).

## Tüketim

Son kullanıcılar `@axrone/physics` şemsiyesi üzerinden de erişebilir
(facade tüm raycast yüzeyini yeniden dışa aktarır).
