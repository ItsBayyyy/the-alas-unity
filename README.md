# The Alas

Survival horror first-person: kumpulkan lima suku cadang mobil (kunci, ban, aki, bensin, oli) dan kabur dari hutan dalam 7:30, sementara penunggu alas memburu lewat suara. Dokumen desain lengkap ada di `The Alas — Dokumen Desain Game.docx` (di luar repo).

- Engine: Unity 6000.3.6f1, URP, Input System
- Scene utama: `Assets/_Project/Scenes/Hutan.unity`
- Kontrol (sesuai layar Pengaturan Citari): WASD jalan · Shift lari · C jongkok · Spasi lompat · E atau klik kiri ambil / setor / nyalakan mesin · 1–9 atau scroll pilih barang · Q buang (jongkok: letakkan pelan) · G atau klik kanan lempar · H kertas tugas · Tab (tahan) daftar kontrol · F layar penuh · M bunyi · Esc jeda · R main lagi di layar akhir. Senter selalu menyala
- Alur: menu utama (Play Game / Setting / Quit) → kartu cerita (Skip) → cutscene → ronde → GAME OVER atau YOU ESCAPE (Ulang langsung main lagi tanpa menu dan cutscene; Menu kembali ke menu utama)
- Cutscene pembuka sekitar 40 detik (komponen `IntroCutscene` di `Systems`): kamera turun ke mobil, mendekati mobil, sepasang mata menyala di hutan, judul dan tagline, lalu sudut pandang pemain (terduduk di samping mobil, berdiri, berbalik ke hutan) yang berakhir tepat di pose kamera pemain sehingga kontrol diserahkan tanpa potongan. Tanpa model karakter. Timer, makhluk, kontrol, dan HUD baru jalan setelah cutscene. Spasi atau Enter untuk melewati. Teks dan durasi tiap shot bisa diubah di Inspector.
- E di mobil tanpa membawa barang saat suku cadang belum lengkap: coba starter. Hanya bunyi starter gagal yang berisik (suara 30), jadi berisiko
- Audio (rekaman Jaya, `Assets/_Project/Audio`): langkah menurut permukaan dan beban (`PlayerAudio`), napas saat kehabisan stamina, geraman dan endusan makhluk dari posisinya (`CreatureAudio`), bunyi ambil dan jatuh barang menurut bahannya (klip di `ITEM_*.asset`), bagasi, pintu, mesin, dan starter mobil, serta ambience hutan, detak jantung yang makin cepat saat makhluk dekat, musik bahaya saat diburu, musik menang, dan bunyi tertangkap (`AtmosphereAudio` di `Systems`). Klip dipasang otomatis oleh builder dari nama file; ganti file `.wav`-nya dengan nama yang sama untuk memperbarui
- F1 (Editor dan development build): status makhluk, jarak, dan suara untuk playtest
- F2 (tombol tersembunyi, aktif di semua build, sengaja tidak ditulis di UI): panggil makhluk ke 10 m di belakang pemain (ke depan kalau belakang di luar peta), untuk demo dan playtest
- UI dari desain Citari (`Assets/_Project/UI/Sprites`, dikumpulkan di `UI_Skin.asset`; mockup asli di `Source/UI`): `MainMenu`, kartu cerita di `IntroCutscene`, `GameHud` (timer, bar Suara dan Nafas, slot, kertas tugas berisi lokasi tiap suku cadang, daftar kontrol, jeda, layar akhir), dan `SettingsPanel` (sensitivitas mouse, kecerahan, volume, sudut pandang, kualitas grafis, balik arah mouse; disimpan di PlayerPrefs, diterapkan `SettingsApplier`). Layar penuh memakai mockup sebagai latar dan potongan merahnya saat tombol disorot. Bagian mikrofon diredupkan karena Unity WebGL tidak bisa membaca mikrofon

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

Assets/ThirdParty/       aset dari luar tim, struktur folder asli paketnya dipertahankan;
                         hanya file yang benar-benar dipakai yang disalin, dan wajib tercatat di CREDITS.md

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
   - Makhluk: model Rafael (`Art/Characters/Creature/SK_Creature.fbx`, rig Mixamo, skala 0,28 supaya tingginya ±2,9 m) dengan `AC_Creature.controller`: blend Idle_01 → Walk → Run menurut kecepatan agen, Idle_02 saat mencari di titik bunyi, tempo animasi ikut kecepatan (`CreatureAnimator`). Makhluknya tanpa mata, jadi penanda keadaan (`EyeL`/`EyeR`, material Unlit, kuning menyelidiki, merah memburu) adalah cahaya di dalam kedua mulutnya, ditempel ke tulang `mixamorig:Head`. Keenam file animasi kiriman Rafael berisi gerakan lari yang sama, jadi diam memakai `ANIM_Creature_Hold.anim` (frame pertama lari) dan jalan memakai lari yang diperlambat. Kalau animasi jalan dan diam yang asli datang, timpa `ANIM_Creature_Walk.fbx` dan pasang klip diamnya di state Search dan anak pertama blend tree `AC_Creature`.
   - Setelah landmark, pohon, atau batang tumbang diganti, bake ulang navmesh: pilih objek `NavMesh` di scene → Bake.
5. Simpan prefab. Semua salinannya di scene langsung ikut berubah.

## Greybox

Menu **Tools → The Alas → Bangun Greybox Hutan** membangun ulang `Hutan.unity` dari nol: peta 160 × 160 m, mobil di tepi utara, enam landmark, sekitar 490 pohon, batang tumbang yang melintang di jalur lurus mobil–landmark, semak, makhluk, sistem ronde, dan navmesh. Isi hutan: pohon (European Forests), semak Hazel, batang tumbang dan ranting bertekstur kulit kayu Spruce, rumput detail, dan jalur setapak melingkar yang melewati landmark. Jalur itu sengaja bebas semak, ranting, dan kerikil (GDD: rute aman tapi lebih panjang). Ranting (`PF_Twig_01`, komponen `Twig`) patah sekali saat diinjak dan menambah suara 30 (12 kalau jongkok) plus 2 per unit beban; belum ada rekaman ranting patah, jadi sementara memakai `SFX_Item_Wood_Drop_01`; simpan rekamannya sebagai `SFX_Twig_Snap_01.wav` lalu ganti nama klip di `GameplaySetup.ConfigureAudio`.

Tanahnya terrain dari `Source/Terrain/Hutan_Height_129.r16`: potongan 160 × 160 m dari lereng bukit tengah map Gung Wah (780 × 889 m), dengan beda tinggi aslinya 12,9 m. Lapisan tanahnya (rumput dan tanah berbatu) juga dari map itu. Sumur Tua memakai model sumur dari map Gung Wah, dan halaman Gubuk Reyot diisi props peternakannya (lumbung, jerami, gerobak dorong, palung, ember, pagar; prefab `PF_Prop_*`). Terrain dan pohon berlanjut 30 m di luar tembok tak terlihat sebagai pemandangan, supaya tepi peta tidak terlihat terpotong; navmesh makhluk dibatasi di dalam tembok; area landmark, mobil, dan titik muncul diratakan otomatis, dan semua objek diletakkan mengikuti kontur. Hapus file itu untuk kembali ke tanah datar. Mobil, pohon, Sumur Tua, dan makhluk sudah memakai model final dan tidak pernah dibuat ulang sebagai placeholder (builder berhenti dengan pesan jelas kalau prefabnya hilang); greybox tinggal untuk landmark yang belum ada modelnya serta Busi dan Jeriken. Isi `Model` di prefab yang sudah ada tidak disentuh; builder hanya menambah komponen gameplay yang belum ada, jadi model final aman. Perubahan manual di scene **hilang** kalau menu ini dijalankan lagi; berhenti memakainya setelah layout dikunci.

## Git

- Repo memakai Git LFS untuk file biner (`.fbx`, `.blend`, `.png`, `.wav`, `.psd`, dan lainnya; lihat `.gitattributes`). Jalankan `git lfs install` sekali di setiap komputer.
- Commit file `.meta` bersama aset aslinya. Aset tanpa `.meta` akan kehilangan semua referensinya di komputer lain.
- Satu orang satu area. Hindari dua orang mengedit `Hutan.unity` atau prefab yang sama di hari yang sama; kabari Bayu dulu.
- Tidak ada aset hasil Generative AI. Setiap aset dari luar dicatat di daftar kredit bersama, lengkap dengan sumber dan lisensinya.
