using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using MLM_Level.Data;

namespace MLM_Level.Services
{
    public static class MemberIdFormatter
    {
        public const string Prefix = "ML";
        public const int DigitLength = 6;

        public static string Format(int sequence) =>
            Prefix + sequence.ToString().PadLeft(DigitLength, '0');

        public static bool IsFormattedMemberId(string? value)
        {
            if (string.IsNullOrWhiteSpace(value) || !value.StartsWith(Prefix))
                return false;

            var numPart = value.Substring(Prefix.Length);
            return numPart.Length == DigitLength
                && numPart.All(char.IsDigit)
                && int.TryParse(numPart, out var n)
                && n > 0;
        }

        public static int ParseSequence(string memberId)
        {
            if (!IsFormattedMemberId(memberId))
                return 0;

            return int.Parse(memberId.Substring(Prefix.Length));
        }
    }

    public interface IMemberIdService
    {
        Task<string> GenerateNextMemberIdAsync();
    }

    public class MemberIdService : IMemberIdService
    {
        private readonly ApplicationDbContext _context;

        public MemberIdService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<string> GenerateNextMemberIdAsync()
        {
            var usernames = await _context.Users
                .Where(u => u.Username.StartsWith(MemberIdFormatter.Prefix))
                .Select(u => u.Username)
                .ToListAsync();

            var maxSeq = usernames
                .Select(MemberIdFormatter.ParseSequence)
                .DefaultIfEmpty(0)
                .Max();

            return MemberIdFormatter.Format(maxSeq + 1);
        }
    }
}
