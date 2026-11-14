// Expo Router retains unfocused screens. Native uses aria-hidden; web uses inert
// from SDK 58's router patch onward. Neither subtree belongs to the active UI.
export const activeRouteSelector =
  ':not([aria-hidden="true"], [aria-hidden="true"] *, [inert], [inert] *)';

export function activeRole(page, role, options) {
  return page.getByRole(role, options).and(page.locator(activeRouteSelector));
}
