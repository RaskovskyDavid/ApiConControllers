namespace ApiConControllers.Entities
{
    // One row per refresh token we hand out. We store these so we can rotate,
    // expire, and revoke them - something we cannot do with a stateless JWT.
    // Only the SHA-256 HASH is stored: if the database leaks, the tokens are useless.
    public class RefreshToken
    {
        public int Id { get; set; }
        public string TokenHash { get; set; } = string.Empty;
        public string UserId { get; set; } = string.Empty;
        public DateTime Created { get; set; }
        public DateTime Expires { get; set; }
        public DateTime? Revoked { get; set; }
        // When this token is rotated, we record the hash of the token that replaced it.
        public string? ReplacedByTokenHash { get; set; }
        // A token is only usable if it has not been revoked and has not expired.
        public bool IsActive => Revoked is null && DateTime.UtcNow < Expires;
    }
}
