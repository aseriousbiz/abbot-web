import { HTMLProps } from "react";

type IconProps = HTMLProps<HTMLSpanElement> & {
    /** A series of Font Awesome class strings. */
    icon: string;
}

/** Renders a font awesome icon in a React-compatible way */
export default function Icon({ icon, ...props }: IconProps) {
    // We wrap the "i" tag in a nested span to avoid an incompatibility between Font Awesome and React.
    // During rendering, Font Awesome removes the 'i' tag and replaces it with SVG.
    // However, if this Icon component is removed from the DOM tree, React will try to remove the root DOM element from it's parent.
    // If that root element is the 'i' tag that Font Awesome removed, React will fail with:
    // "Failed to execute 'removeChild' on 'Node': The node to be removed is not a child of this node."
    // So, we use a dummy Span that doesn't get removed so that React has something to yeet.

    // We also set 'key' to 'icon' so that if the icon changes, React will replace the entire span.
    return <span key={icon} {...props}><i className={icon} /></span>;
}