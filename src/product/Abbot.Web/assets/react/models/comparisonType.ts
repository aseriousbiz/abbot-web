const ComparisonTypes = [
    'StartsWith',
    'EndsWith',
    'Contains',
    'RegularExpression',
    'ExactMatch',
    'GreaterThan',
    'LessThan',
    'GreaterThanOrEqualTo',
    'LessThanOrEqualTo',
    'Equals',
    'Exists',
    'All',
    'Any']
export type ComparisonType = typeof ComparisonTypes[number];

export interface ComparisonTypeOption {
    value: ComparisonType;
    label: string;
    title?: string;
}
