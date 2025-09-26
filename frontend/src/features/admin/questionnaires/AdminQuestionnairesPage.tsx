import { useMemo } from "react";
import { useAllQuestionnaires, usePublishLatestInFamily } from "./adminQueries";

export default function AdminQuestionnairesPage() {
  const { data, isLoading, error } = useAllQuestionnaires();
  const publish = usePublishLatestInFamily();

  const families = useMemo(() => {
    const map = new Map<string, { canonicalId: string; versions: any[] }>();
    (data ?? []).forEach(q => {
      const key = q.canonicalId;
      if (!map.has(key)) map.set(key, { canonicalId: key, versions: [] });
      map.get(key)!.versions.push(q);
    });
    // sort by version DESC inside each family
    for (const f of map.values()) f.versions.sort((a,b) => b.version - a.version);
    return Array.from(map.values());
  }, [data]);

  if (isLoading) return <div className="p-6">Loading…</div>;
  if (error) return <div className="p-6 text-red-600">Failed to load.</div>;

  return (
    <div className="p-6 max-w-4xl mx-auto space-y-6">
      <h1 className="text-2xl font-semibold">Questionnaires (Admin)</h1>
      {families.map(f => {
        const latest = f.versions[0];
        const active = f.versions.find(v => v.status === "active");
        const canPublish = latest && active && latest.id !== active.id;

        return (
          <div key={f.canonicalId} className="border rounded-xl p-4">
            <div className="flex items-center justify-between">
              <div>
                <div className="font-medium">Family: {f.canonicalId}</div>
                <div className="text-sm text-gray-600">
                  Active: v{active?.version ?? "-"} ({active?.title ?? "—"})
                </div>
              </div>
              <button
                className="px-3 py-1.5 rounded-lg border disabled:opacity-50"
                disabled={!canPublish || publish.isPending}
                onClick={async () => {
                  if (!confirm(`Publish latest (v${latest.version}) for this family?`)) return;
                  await publish.mutateAsync(f.canonicalId);
                  alert("Published.");
                }}
              >
                {publish.isPending ? "Publishing…" : "Publish latest"}
              </button>
            </div>

            <div className="mt-3 text-sm">
              Versions:&nbsp;
              {f.versions.map(v => (
                <span key={v.id} className="mr-3">
                  v{v.version} {v.status === "active" ? "🔥" : ""} ({v.status})
                </span>
              ))}
            </div>
          </div>
        );
      })}
    </div>
  );
}
