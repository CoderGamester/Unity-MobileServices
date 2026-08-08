using System;
using GameLovers.MobileServices.Editor.Explorer.Overlays;
using GameLovers.MobileServices.Notifications;

// ReSharper disable once CheckNamespace
namespace GameLovers.MobileServices.Editor.Simulation
{
	/// <summary>Editor adapter for a live notification service owned by a sample or consumer.</summary>
	public interface IMobileNotificationSimulationTarget
	{
		/// <summary>Name shown in the Device Simulator connection status.</summary>
		string DisplayName { get; }

		/// <summary>Number of notifications currently pending in the target service.</summary>
		int PendingCount { get; }

		/// <summary>Delivers the earliest pending notification regardless of its scheduled time.</summary>
		bool TryDeliverNext(out SimulatedNotificationBannerSpec spec);

		/// <summary>Delivers the earliest pending notification whose scheduled time has elapsed.</summary>
		bool TryDeliverDue(out SimulatedNotificationBannerSpec spec);
	}

	/// <summary>Transient editor bridge between the Device Simulator and an active notification service.</summary>
	public static class MobileNotificationSimulation
	{
		/// <summary>Raised when the active simulation target changes.</summary>
		public static event Action ActiveTargetChanged;

		/// <summary>The explicitly registered target, or <c>null</c> when no sample is active.</summary>
		public static IMobileNotificationSimulationTarget ActiveTarget { get; private set; }

		/// <summary>Registers the target that owns notification scheduling for the active sample.</summary>
		public static void Register(IMobileNotificationSimulationTarget target)
		{
			if (target == null || ReferenceEquals(ActiveTarget, target))
			{
				return;
			}

			ActiveTarget = target;
			ActiveTargetChanged?.Invoke();
		}

		/// <summary>Removes the target only when it is still the active registration.</summary>
		public static void Unregister(IMobileNotificationSimulationTarget target)
		{
			if (!ReferenceEquals(ActiveTarget, target))
			{
				return;
			}

			ActiveTarget = null;
			ActiveTargetChanged?.Invoke();
		}

		/// <summary>Delivers the next pending target notification and paints its exact banner payload.</summary>
		public static bool TryDeliverNext()
		{
			var target = ActiveTarget;
			if (target == null || !target.TryDeliverNext(out var spec))
			{
				return false;
			}

			MobileSimulatorState.PushNotificationBanner(spec);
			return true;
		}

		/// <summary>Delivers one due target notification and paints its exact banner payload.</summary>
		public static bool TryDeliverDue()
		{
			var target = ActiveTarget;
			if (target == null || !target.TryDeliverDue(out var spec))
			{
				return false;
			}

			MobileSimulatorState.PushNotificationBanner(spec);
			return true;
		}

		/// <summary>Advances the connected target by at most one due notification while the simulator is enabled.</summary>
		public static void Tick()
		{
			if (MobileSimulatorState.Enabled)
			{
				TryDeliverDue();
			}
		}

		/// <summary>Invokes the runtime service's explicit editor delivery path.</summary>
		public static bool TryDeliver(INotificationService service, int notificationId)
		{
			return service is MobileNotificationService mobileService &&
				mobileService.TrySimulateDelivery(notificationId);
		}
	}
}
