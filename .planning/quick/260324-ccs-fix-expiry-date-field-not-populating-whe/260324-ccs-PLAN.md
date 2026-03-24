---
phase: quick
plan: 260324-ccs
type: execute
wave: 1
depends_on: []
files_modified:
  - FrontEnd/links.js
autonomous: true
requirements: []
must_haves:
  truths:
    - "Clicking an expiry preset button populates the datetime-local input with the calculated date"
    - "Clicking a count preset button populates the custom count input with the selected value"
    - "Custom input still overrides preset selection when user types directly"
  artifacts:
    - path: "FrontEnd/links.js"
      provides: "Upload slot form with synced preset and custom inputs"
  key_links:
    - from: "preset-btn click handler (expiry)"
      to: "uploadSlotCustomExpiry input"
      via: "setting .value to ISO datetime-local format"
      pattern: "uploadSlotCustomExpiry.*value"
---

<objective>
Fix expiry date and file count input fields not reflecting values when quick-select preset buttons are clicked in the upload slot creation form.

Purpose: When a user clicks "7 days" or "30 days" (etc.), the datetime-local input should show the calculated expiry date, and similarly count preset buttons should populate the count input. Currently the inputs remain empty, which is confusing.

Output: Updated FrontEnd/links.js with preset-to-input synchronization.
</objective>

<execution_context>
@$HOME/.claude/get-shit-done/workflows/execute-plan.md
@$HOME/.claude/get-shit-done/templates/summary.md
</execution_context>

<context>
@FrontEnd/links.js
@FrontEnd/links.html

<interfaces>
<!-- The bug is in two click handlers in links.js -->

Expiry preset handler (lines 211-218):
```javascript
document.getElementById("uploadSlotExpiryPresets").querySelectorAll(".preset-btn").forEach(btn => {
    btn.addEventListener("click", () => {
        const hours = parseInt(btn.dataset.hours);
        selectedSlotExpiry = new Date(Date.now() + hours * 3600000);
        document.getElementById("uploadSlotExpiryPresets").querySelectorAll(".preset-btn").forEach(b => b.style.borderColor = "");
        btn.style.borderColor = "var(--accent)";
        document.getElementById("uploadSlotCustomExpiry").value = "";  // BUG: clears instead of populating
    });
});
```

Count preset handler (lines 229-236):
```javascript
document.getElementById("uploadSlotCountPresets").querySelectorAll(".preset-btn").forEach(btn => {
    btn.addEventListener("click", () => {
        selectedSlotCount = parseInt(btn.dataset.count);
        document.getElementById("uploadSlotCountPresets").querySelectorAll(".preset-btn").forEach(b => b.style.borderColor = "");
        btn.style.borderColor = "var(--accent)";
        document.getElementById("uploadSlotCustomCount").value = "";  // BUG: clears instead of populating
    });
});
```

The `datetime-local` input requires values in `YYYY-MM-DDTHH:mm` format.
The count input is a simple number input.
</interfaces>
</context>

<tasks>

<task type="auto">
  <name>Task 1: Sync preset buttons to custom input fields</name>
  <files>FrontEnd/links.js</files>
  <action>
Fix two preset button click handlers in FrontEnd/links.js:

1. **Expiry preset handler** (around line 217): Instead of clearing `uploadSlotCustomExpiry.value` to `""`, set it to the calculated expiry date formatted for `datetime-local` input. The format must be `YYYY-MM-DDTHH:mm`. Use the already-computed `selectedSlotExpiry` Date object:
   ```javascript
   // Replace: document.getElementById("uploadSlotCustomExpiry").value = "";
   // With: format selectedSlotExpiry as local datetime-local string
   const d = selectedSlotExpiry;
   const pad = n => String(n).padStart(2, '0');
   document.getElementById("uploadSlotCustomExpiry").value =
       `${d.getFullYear()}-${pad(d.getMonth()+1)}-${pad(d.getDate())}T${pad(d.getHours())}:${pad(d.getMinutes())}`;
   ```

2. **Count preset handler** (around line 235): Instead of clearing `uploadSlotCustomCount.value` to `""`, set it to the selected count value:
   ```javascript
   // Replace: document.getElementById("uploadSlotCustomCount").value = "";
   // With:
   document.getElementById("uploadSlotCustomCount").value = btn.dataset.count;
   ```

Do NOT change any other behavior. The custom input change/input handlers (lines 221-241) that allow manual override should remain as-is.
  </action>
  <verify>
    <automated>grep -n "uploadSlotCustomExpiry.*value" FrontEnd/links.js | grep -v '= ""' | grep -c "getFullYear\|pad"</automated>
  </verify>
  <done>Clicking any expiry preset button (1hr, 24hrs, 7 days, 30 days) populates the datetime-local input with the calculated date. Clicking any count preset (1, 5, 10, 25) populates the number input with that count. Manual entry in custom fields still works and overrides presets.</done>
</task>

</tasks>

<verification>
1. Open links.html in browser, authenticate, click "New upload slot"
2. Click "7 days" preset -- the datetime-local input should show a date ~7 days from now
3. Click "30 days" preset -- input updates to ~30 days from now
4. Click count "5" -- the count input shows 5
5. Type a custom date manually -- it still works and overrides
6. Create a slot successfully -- all form logic unchanged
</verification>

<success_criteria>
- Expiry preset buttons populate the datetime-local input with the correct calculated date
- Count preset buttons populate the number input with the selected value
- Custom manual input still works and overrides presets
- Slot creation API call still sends correct data
</success_criteria>

<output>
After completion, create `.planning/quick/260324-ccs-fix-expiry-date-field-not-populating-whe/260324-ccs-SUMMARY.md`
</output>
