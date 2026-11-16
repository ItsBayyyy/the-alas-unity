# The Alas

Survival horror first-person: kumpulkan empat suku cadang mobil dan kabur dari hutan dalam 7:30, sementara penunggu alas memburu lewat suara. Dokumen desain lengkap ada di `The Alas — Dokumen Desain Game.docx` (di luar repo).

- Engine: Unity 6000.3.6f1, URP, Input System
- Scene utama: `Assets/_Project/Scenes/Hutan.unity`
- Kontrol: WASD jalan · Shift lari · C jongkok · Spasi lompat · F senter · E ambil / setor / nyalakan mesin · 1–9 atau scroll pilih barang · Q buang (jongkok: letakkan pelan) · G atau klik kanan lempar · Esc lepas kursor · R main lagi di layar akhir
- F1 (Editor dan development build): status makhluk, jarak, dan suara untuk playtest
- F2 (Editor dan development build): panggil makhluk ke 20 m di belakang pemain (ke depan kalau belakang di luar peta)
- HUD saat ini adalah `TemporaryHud` (IMGUI) dan akan diganti HUD dari Citari

## Struktur folder

```
Assets/_Project/
├── Art/
│   ├── Characters/      Rafael   — makhluk, tangan pemain (FBX, tekstur, animasi)
│   ├── Environment/     Gung Wah — pohon, semak, batang tumbang, batu, landmark
│   ├── Props/           Gung Wah — suku cadang dan barang antik
│   ├── Vehicle/         Gung Wah — mobil
│   ├── Materials/       material final; Greybox/ berisi material sementara
│   └── Textures/
├── Audio/
│   ├── SFX/             Jaya — langkah, barang, mobil, makhluk, UI
│   ├── Ambience/        Jaya
│   ├── Music/           Jaya
│   └── Mixers/          Jaya
├── UI/
│   ├── Fonts/           Citari — font OFL beserta file lisensinya
│   └── Sprites/         Citari — HUD, ikon, kertas, logo
├── Data/Items/          Bayu — bobot, nama, dan nilai tiap barang (ITEM_*.asset)
├── Prefabs/             Bayu — dirakit dari aset di atas
│   ├── Player/  Vehicle/  Environment/  Landmarks/  Items/  Creature/
├── Scenes/
└── Scripts/             Bayu dan Jaya (sistem audio, mikrofon)
    ├── Core/  Player/  Items/  Creature/  World/  UI/  Editor/

Source/                  file mentah, bukti karya dibuat setelah 10 November
├── Blender/             .blend
├── Audio/               rekaman mentah, proyek Audacity/Reaper
└── UI/                  .psd, .kra, scan kertas
```

File mentah **jangan** dimasukkan ke `Assets/`. Unity akan mencoba mengimpor `.blend` dan itu gagal di komputer yang tidak punya Blender. Ekspor ke FBX dulu, lalu taruh FBX-nya di `Assets/_Project/Art/...`.

## Aturan penamaan

Format: `Prefiks_NamaPascalCase_Varian`. Tanpa spasi, tanpa huruf kecil di awal kata, nomor varian dua digit.

| Jenis | Prefiks | Contoh |
|---|---|---|
| Prefab | `PF_` | `PF_Landmark_SumurTua`, `PF_Tree_Jati_02` |
| Mesh statis (FBX) | `SM_` | `SM_Tree_Pinus_01`, `SM_Item_Aki` |
| Mesh dengan rig | `SK_` | `SK_Creature`, `SK_PlayerHands` |
| Animasi | `ANIM_` | `ANIM_Creature_Hunt`, `ANIM_Hands_HoldHeavy` |
| Material | `M_` | `M_Bark_Jati` |
| Tekstur | `T_` + akhiran peta | `T_Bark_Jati_BaseColor`, `T_Bark_Jati_Normal` |
| Efek suara | `SFX_` | `SFX_Step_Gravel_Run_03`, `SFX_Item_Metal_Drop_01` |
| Ambience | `AMB_` | `AMB_Forest_Night_Loop` |
| Musik | `MUS_` | `MUS_Drone_Danger` |
| Sprite UI | `UI_` | `UI_Icon_Busi`, `UI_Panel_Paper` |
| Data barang | `ITEM_` | `ITEM_Aki`, `ITEM_Keris` |
| Scene | tanpa prefiks | `Hutan`, `MainMenu` |
| Skrip | nama kelas | `PlayerMotor.cs` |

Nama dalam bahasa Inggris untuk kategori (`Tree`, `Item`, `Step`), bahasa Indonesia untuk nama khas game (`Jati`, `SumurTua`, `Kentongan`).

Skala: 1 unit Unity = 1 meter. Ekspor dari Blender dengan pivot di dasar objek (menyentuh tanah) dan menghadap +Z (sumbu depan Unity).

## Mengganti placeholder dengan aset final

Setiap prefab placeholder punya struktur yang sama:

```
PF_Landmark_SumurTua        ← root: pivot di tanah, berisi komponen gameplay
└── Model                   ← visual greybox; ganti isinya saja
```

1. Buka prefab (klik dua kali di folder `Prefabs`).
2. Hapus semua anak di dalam `Model`, lalu seret mesh final ke dalam `Model`.
3. Untuk landmark dan mobil, beri mesh final collider sendiri (Mesh Collider atau beberapa Box Collider). Collider greybox ikut terhapus bersama anak `Model`.
4. Jangan ubah komponen di root. Collider batang tumbang (tinggi 0,45 m), trigger semak, trigger Kali Kering, Rigidbody barang, dan NavMeshAgent makhluk adalah bagian gameplay.
   - Landmark: titik muncul suku cadang ada di anak `ItemSpawns`. Kalau bentuk model berubah, geser titiknya supaya tetap di tanah dan tidak tertanam di dinding.
   - Makhluk: mata harus tetap bernama `EyeL` dan `EyeR` dengan material yang punya emission, supaya warnanya bisa berubah kuning dan merah.
   - Setelah landmark, pohon, atau batang tumbang diganti, bake ulang navmesh: pilih objek `NavMesh` di scene → Bake.
5. Simpan prefab. Semua salinannya di scene langsung ikut berubah.

## Greybox

Menu **Tools → The Alas → Bangun Greybox Hutan** membangun ulang `Hutan.unity` dari nol: peta 160 × 160 m, mobil di tepi utara, enam landmark, sekitar 490 pohon, batang tumbang yang melintang di jalur lurus mobil–landmark, semak, makhluk, sistem ronde, dan navmesh. Isi `Model` di prefab yang sudah ada tidak disentuh; builder hanya menambah komponen gameplay yang belum ada, jadi model final aman. Perubahan manual di scene **hilang** kalau menu ini dijalankan lagi; berhenti memakainya setelah layout dikunci.

## Git

- Repo memakai Git LFS untuk file biner (`.fbx`, `.blend`, `.png`, `.wav`, `.psd`, dan lainnya; lihat `.gitattributes`). Jalankan `git lfs install` sekali di setiap komputer.
- Commit file `.meta` bersama aset aslinya. Aset tanpa `.meta` akan kehilangan semua referensinya di komputer lain.
- Satu orang satu area. Hindari dua orang mengedit `Hutan.unity` atau prefab yang sama di hari yang sama; kabari Bayu dulu.
- Tidak ada aset hasil Generative AI. Setiap aset dari luar dicatat di daftar kredit bersama, lengkap dengan sumber dan lisensinya.
