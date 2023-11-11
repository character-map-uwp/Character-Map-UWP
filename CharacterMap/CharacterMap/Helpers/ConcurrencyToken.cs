namespace CharacterMap.Helpers;

public class ConcurrencyToken
{
    public class ConcurrencyTokenGenerator
    {
        private object Root { get; } = new object();
        private Guid CurrentId { get; set; } = Guid.NewGuid();

        public ConcurrencyToken GenerateToken()
        {
            lock (Root)
            {
                CurrentId = new Guid();
                return new ConcurrencyToken(CurrentId, this);
            }
        }

        internal bool IsValid(ConcurrencyToken token)
        {
            lock (Root)
            {
                return token.Id == CurrentId;
            }
        }
    }

    private Guid Id { get; }
    private ConcurrencyTokenGenerator Owner { get; }

    private ConcurrencyToken(Guid id, ConcurrencyTokenGenerator owner)
    {
        Id = id;
        Owner = owner;
    }

    public bool IsValid()
    {
        return Owner.IsValid(this);
    }
}
