using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace Libs.Extensions
{
    public static class TaskExtensions
    {
        /// <summary>
        /// Empty method to mute 'call is not awaited' warning.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void NoWait(this Task task)
        {
        }

        /// <summary>
        /// Empty method to mute 'call is not awaited' warning.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void NoWait<T>(this Task<T> task)
        {
        }

        /// <summary>
        /// Alternative to standard task.Result. It unwraps exceprion, that makes stack trace more readable.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T GetResult<T>(this Task<T> task)
        {
            return task.GetAwaiter().GetResult();
        }

        /// <summary>
        /// Alternative to standard task.Result. It unwraps exceprion, that makes stack trace more readable.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void GetResult(this Task task)
        {
            task.GetAwaiter().GetResult();
        }
    }
}
