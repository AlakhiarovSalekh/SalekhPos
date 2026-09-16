export type NotificationItem = {
  id: string;
  branchId: string | null;
  recipientSubject: string;
  title: string;
  body: string;
  severity: "info" | "warning" | "critical";
  isRead: boolean;
  createdAt: string;
  readAt: string | null;
};

export type NotificationPage = {
  items: NotificationItem[];
  nextCursor: string | null;
};

export type NotificationPreferences = {
  inAppEnabled: boolean;
  emailEnabled: boolean;
  pushEnabled: boolean;
  updatedAt: string;
};
