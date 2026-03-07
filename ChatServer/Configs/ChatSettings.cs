using System.ComponentModel.DataAnnotations;

namespace ChatServer.Configs
{
    public class ChatSettings
    {
        [Required(AllowEmptyStrings = false, ErrorMessage = "DataFolderPath is required")]
        public string DataFolderPath { get; init; }
    }
}
