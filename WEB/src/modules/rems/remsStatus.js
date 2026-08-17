// The REMS request lifecycle codes, mirroring RemsRequestStatuses on the server.
//
// The codes are what the option set stores, so two of them read oddly and are kept deliberately:
// `customer_submitted` is the ADMIN REVIEW stage (the client has submitted; the admin now reviews), and
// there is no `submitted` at all — the Admin Pool it named is gone, because the initiator sends the
// intake link to the client themselves.
//
// The lifecycle names the stage a request is IN — who it is waiting on — rather than the event that last
// happened to it.
export const REMS_STATUS = Object.freeze({
  /** With its initiator, not yet sent to the client. */
  DRAFT: "draft",
  /** The intake link has been emailed; the ball is with the client. */
  AWAITING_CUSTOMER: "awaiting_customer",
  /** The client's answers are in; the named admin is reviewing them. */
  ADMIN_REVIEW: "customer_submitted",
  /** The admin returned the engagement setup for rework, with a reason. */
  RETURNED_TO_INITIATOR: "returned_to_initiator",
  /** The initiator revised the setup; back with the admin to confirm. */
  AWAITING_ADMIN_CONFIRMATION: "awaiting_admin_confirmation",
  /** Routed to the approvers. Every field is read-only while a round is open. */
  PENDING_APPROVAL: "pending_approval",
  /** Enough approvers declined to close the round; back with the INITIATOR to rework the setup. */
  CHANGES_REQUESTED: "changes_requested",
  /** Fully approved (terminal). */
  APPROVED: "approved"
});

/** The two stages where the setup is with its initiator and only the setup may be edited. */
export const REMS_REWORK_STATUSES = Object.freeze([
  REMS_STATUS.RETURNED_TO_INITIATOR,
  REMS_STATUS.CHANGES_REQUESTED
]);
