namespace OpenCvStudy.Services
{
    public interface IDialogService
    {
        string SelectImageFile();

        void ShowError(string message, string title);
    }
}
