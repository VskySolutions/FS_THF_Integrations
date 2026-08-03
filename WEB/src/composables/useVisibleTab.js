import { ref, onActivated, onDeactivated } from "vue";

/**
 * True while this component is the tab actually on screen.
 *
 * Tab panels are kept alive, so a tab you have switched away from stays mounted and keeps rendering —
 * which matters the moment it puts something into shared chrome (a button teleported into a card
 * header, say), because every tab you ever opened would pile up there. Activation is the only reliable
 * signal for that; a plain v-if on the panel cannot see it.
 *
 * Starts true: a keep-alive panel is not rendered until it is first shown.
 */
export function useVisibleTab () {
  const isVisibleTab = ref(true);
  onActivated(() => { isVisibleTab.value = true; });
  onDeactivated(() => { isVisibleTab.value = false; });
  return { isVisibleTab };
}
