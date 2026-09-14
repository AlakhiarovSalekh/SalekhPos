import { Redirect } from "expo-router";

import { useSession } from "@/state/SessionContext";

export default function IndexRoute() {
  const { status } = useSession();
  return <Redirect href={status === "authenticated" ? "/dashboard" : "/sign-in"} />;
}
