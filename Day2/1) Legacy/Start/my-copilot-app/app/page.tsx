"use client";
import { CopilotSidebar } from "@copilotkit/react-ui";
export default function Page() {
  return (
    <main>
      <CopilotSidebar
        labels={{
          title: "Your Assistant",
          initial: "Hi! How can I help you today?",
        }}
      />
      <h1>Your App</h1>
    </main>
  );
}