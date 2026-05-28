using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Domain.Models
{
    public class RefreshToken : BaseModel<Guid>
    {
        public string Token { get; set; }
        public DateTime ExpiresAt { get; set; }
        public bool IsExpired { get; set; }
        public User User { get; set; }
        public RefreshToken(string token, DateTime expiresAt, User user)
        {
            Id = Guid.NewGuid();
            Token = token;
            ExpiresAt = expiresAt;
            IsExpired = false;
            User = user;
        }
        public RefreshToken()
        {

        }
        public void Invalidate()
        {
            if (!IsExpired)
                IsExpired = true;
        }
    }
}
