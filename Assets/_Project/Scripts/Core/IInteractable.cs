using TheAlas.Player;

namespace TheAlas.Core
{
    /// <summary>Sesuatu yang bisa dipakai pemain dengan tombol E (barang, mobil).</summary>
    public interface IInteractable
    {
        /// <summary>Teks di bawah crosshair. Null kalau tidak ada yang perlu ditampilkan.</summary>
        string GetPrompt(PlayerInteractor who);

        bool CanInteract(PlayerInteractor who);

        void Interact(PlayerInteractor who);
    }
}
