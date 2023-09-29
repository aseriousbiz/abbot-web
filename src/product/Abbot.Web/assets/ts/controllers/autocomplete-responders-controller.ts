import * as autoComplete from '@tarekraafat/autocomplete.js';
import { Member } from '../api/internal';
import { Controller } from "@hotwired/stimulus";

class Time {
    constructor(public hours: number, public minutes: number) {
    }

    static parse(time: string): Time | null {
        const splat = time.split(':');
        if (splat.length != 2) {
            return null;
        }
        const hours = parseInt(splat[0]);
        const minutes = parseInt(splat[1]);
        return new Time(hours, minutes);
    }

    toString(): string {
        // Render in 12hr time
        let hours = this.hours % 12;
        const meridian = this.hours >= 12 ? 'pm' : 'am';
        if (hours == 0) {
            hours = 12;
        }
        return `${hours}:${this.minutes < 10 ? '0' + this.minutes : this.minutes}${meridian}`;
    }

    static equals(left?: Time, right?: Time) {
        return (left && right) && (left.hours == right.hours && left.minutes == right.minutes);
    }
}

export class WorkingHours {
    constructor(public start: Time, public end: Time) {
    }

    static fromJson(j?: { start?: string, end?: string }): WorkingHours | null {
        if (!j || !j.start || !j.end) {
            return null;
        }
        const start = Time.parse(j.start);
        const end = Time.parse(j.end);
        if (!start || !end) {
            return null;
        }
        return new WorkingHours(start, end);
    }

    toString(): string {
        return `${this.start.toString()} - ${this.end.toString()}`;
    }

    static equals(left?: WorkingHours, right?: WorkingHours) {
        return (left && right) && (Time.equals(left.start, right.start) && Time.equals(left.end, right.end));
    }
}

export default class extends Controller<HTMLElement> {
    static targets = ["input"];
    declare readonly inputTarget: HTMLInputElement;

    connect() {
        const input = this.inputTarget;
        const hidden = document.createElement("input");
        hidden.type = "hidden";
        hidden.name = (input.name || input.id) + "Id";
        input.form.appendChild(hidden);

        const existingMembers = input.dataset.members
            ? document.querySelector(input.dataset.members)
            : null;

        new autoComplete({
            debounce: 300,
            selector: () => input,
            placeHolder: "Search for a user…",
            data: {
                src: async (input) => {
                    const response = await fetch(`/api/internal/members/find?q=${input}&role=Agent`);
                    if (!response.ok) {
                        return [];
                    }
                    let users: Member[] = await response.json();
                    if (existingMembers) {
                        users = users.filter(u => {
                            const matchingMember = existingMembers.querySelector(`[data-member-id="${u.id}"]`);
                            return !matchingMember
                        });
                    }
                    return users;
                },
                keys: ['searchKey']
            },
            resultsList: {
                tabSelect: true,
                noResults: true,
                class: "user-selector-popup -ml-2 mt-1",
                element: (list: HTMLUListElement, data: { query: string, matches: Member[], results: Member[] }) => {
                    if (data.results.length == 0) {
                        const item = document.createElement("li");
                        item.className = "user-selector-no-results";
                        item.innerText = "No results found";
                        list.replaceChildren(item);
                    }
                }
            },
            resultItem: {
                selected: "user-selector-item-selected",
                element: (item: HTMLLIElement, data: { match: string, value: Member }) => {
                    const workingHours = WorkingHours.fromJson(data.value.workingHours);
                    const workingHoursInYourTz = WorkingHours.fromJson(data.value.workingHoursInYourTimeZone);

                    item.className = "user-selector-item";

                    const div = document.createElement("div");
                    div.className = "user-selector-item-container";
                    item.replaceChildren(div);

                    const img = document.createElement("img");
                    img.src = data.value.avatarUrl;
                    img.className = "user-selector-item-avatar";
                    div.appendChild(img);

                    const nameSpan = document.createElement("span");
                    nameSpan.className = "user-selector-item-name";
                    nameSpan.innerText = data.value.nickName;
                    div.appendChild(nameSpan);

                    const workingHoursDiv = document.createElement("div");
                    workingHoursDiv.className = "user-selector-item-working-hours";
                    div.appendChild(workingHoursDiv);

                    const labelSpan = document.createElement("span");
                    labelSpan.className = "user-selector-item-working-hours-label";
                    labelSpan.innerText = "Working Hours";
                    workingHoursDiv.appendChild(labelSpan);

                    const hoursSpan = document.createElement("span");
                    hoursSpan.className = workingHoursInYourTz ? "user-selector-item-working-hours-value" : "user-selector-item-working-hours-value italic";
                    hoursSpan.innerText = workingHoursInYourTz ? workingHoursInYourTz.toString() : "Not set";
                    workingHoursDiv.appendChild(hoursSpan);

                    // If the user is in a different timezone, show the working hours in their timezone
                    if (workingHours && !WorkingHours.equals(workingHoursInYourTz, workingHours)) {
                        const localTimeSpan = document.createElement("span");
                        localTimeSpan.className = "user-selector-item-working-hours-label";
                        localTimeSpan.innerText = `(${workingHours.toString()} ${data.value.timeZoneId})`;
                        workingHoursDiv.appendChild(localTimeSpan);
                    }
                }
            },
            events: {
                input: {
                    selection: (evt: CustomEvent) => {
                        input.value = evt.detail.selection.value.nickName;
                        hidden.value = evt.detail.selection.value.id;
                        input.readOnly = true;
                        hidden.form.requestSubmit();
                    },
                },
            },
        });
    }
}
