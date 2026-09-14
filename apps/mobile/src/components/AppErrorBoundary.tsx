import { Component, type ErrorInfo, type PropsWithChildren, type ReactNode } from "react";

import { ErrorSurface } from "./primitives";
import { useLocalization } from "@/localization/LocalizationProvider";

type State = Readonly<{ error: Error | null }>;

export class AppErrorBoundary extends Component<PropsWithChildren, State> {
  state: State = { error: null };

  static getDerivedStateFromError(error: Error): State {
    return { error };
  }

  componentDidCatch(_error: Error, _info: ErrorInfo): void {
    // Telemetry integration belongs here; secrets and session tokens must never be recorded.
  }

  private reset = () => this.setState({ error: null });

  render(): ReactNode {
    if (this.state.error !== null) {
      return <ErrorBoundaryFallback onRetry={this.reset} />;
    }
    return this.props.children;
  }
}

function ErrorBoundaryFallback({ onRetry }: Readonly<{ onRetry: () => void }>) {
  const { t } = useLocalization();
  return <ErrorSurface title={t("error.title")} message={t("error.message")} onRetry={onRetry} />;
}
