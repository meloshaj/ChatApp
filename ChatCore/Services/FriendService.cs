using ChatCore.Models;

namespace ChatCore.Services
{
    public class FriendService
    {
        public bool AddFriend(User user, User friend)
        {
            if (user.Id == friend.Id)
            {
                return false;
            }

            bool addedToUser = user.FriendIds.Add(friend.Id);
            bool addedToFriend = friend.FriendIds.Add(user.Id);
            return addedToUser || addedToFriend;
        }

        public bool AreFriends(User firstUser, User secondUser)
        {
            return firstUser.FriendIds.Contains(secondUser.Id) && secondUser.FriendIds.Contains(firstUser.Id);
        }
    }
}
