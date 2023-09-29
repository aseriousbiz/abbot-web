import usePlaybook from "../hooks/usePlaybooks";
import useDebug from "../hooks/useDebug";
import useFeatureFlags from "../hooks/useFeatureFlags";

export default function DebugPanel() {
    const { playbook, dirty } = usePlaybook();
    const { debug, setDebug } = useDebug();
    const { activeFeatureFlags } = useFeatureFlags();

    if (!debug) {
        return <div className="absolute z-10 top-20 left-4">
            <div className="rounded-lg p-2 bg-white border shadow-sm">
                <button className="btn place-self-start" onClick={() => setDebug(true)} title="Show Staff Mode">
                    <i className="fa-light fa-bug"></i>
                </button>
            </div>
        </div>
    }

    return <div className="absolute z-10 top-20 left-4">
        <div className="rounded-lg p-2 bg-white border shadow-sm flex flex-col gap-2">
            <div className="flex gap-2 items-center">
                <button className="btn place-self-start" onClick={() => setDebug(false)} title="Hide Staff Mode">
                    <i className="fa fa-bug"></i>
                </button>
                Debug Mode On
            </div>
            <details>
                <summary className="font-semibold">
                    Current Definition
                    {dirty && <span className="text-red-500"> (unsaved)</span>}
                </summary>
                <pre className="w-96 overflow-auto">{playbook.toString()}</pre>
            </details>
            <details>
                <summary className="font-semibold">
                    Active Feature Flags
                </summary>
                <ul className="list-disc">
                    {activeFeatureFlags.map(flag => (
                        <li className="ml-4" key={flag}>
                            <code className="text-xs">{flag}</code>
                        </li>
                    ))}
                </ul>
            </details>
        </div>
    </div>;
}