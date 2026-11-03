export function MaintenancePage() {
  return (
    <div className="grid min-h-screen place-items-center bg-[#101820] px-6 text-white">
      <div className="max-w-lg text-center">
        <div className="mx-auto grid h-14 w-14 place-items-center rounded-2xl bg-brass font-display text-xl font-bold">WT</div>
        <h1 className="mt-6 font-display text-3xl font-semibold">Server under maintenance</h1>
        <p className="mt-3 text-[#d5cec2]">WorkTracker is unavailable while an update is in progress. Please wait a few minutes, then refresh this page.</p>
      </div>
    </div>
  );
}
