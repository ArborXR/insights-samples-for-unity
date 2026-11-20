using InsightsSamples.Common;
using UnityEngine;

namespace InsightsSamples.UserData
{
    /// <summary>
    /// Sample 10: identify the learner with SetUserData, and read back what Insights knows with GetUserData and
    /// GetUserId. Identity lives in userData, never in event metadata.
    /// </summary>
    public class UserDataSample : MonoBehaviour
    {
        [SerializeField] private BadgeReader station;

        private void OnEnable()
        {
            station.EmployeeIdentified += HandleEmployeeIdentified;
            station.UserDataCheckRequested += HandleUserDataCheckRequested;
        }

        private void OnDisable()
        {
            station.EmployeeIdentified -= HandleEmployeeIdentified;
            station.UserDataCheckRequested -= HandleUserDataCheckRequested;
        }

        private void HandleEmployeeIdentified(Employee employee)
        {
            // The id is the learner's primary key in Insights reports; the other fields describe them. Every
            // record in the session is tied to this user, so events never need to carry who it was.
            var userData = new Abxr.Dict
            {
                ["first_name"] = employee.FirstName,
                ["site_id"] = employee.SiteId,
            };
            // SetUserData syncs by signing in again in the background, so it needs a signed-in session, and it's
            // ignored (with an SDK warning) while a sign-in or an earlier SetUserData sync is still running. If you
            // also start a new session for this learner, call StartNewSession() first and SetUserData once
            // OnAuthCompleted reports success (sample 15): a new session clears user data.
            Abxr.SetUserData(employee.Id, userData);
            station.Welcome(employee);
        }

        private void HandleUserDataCheckRequested()
        {
            // GetUserData returns a copy of the user data from the last sign-in, or null when not signed in.
            // GetUserId returns its id, or the session's anonymous id when there isn't one.
            station.ShowUserData(Abxr.GetUserId(), Abxr.GetUserData());
        }
    }
}
