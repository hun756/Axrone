import { Component, script } from '@axrone/ecs-runtime';

@script({
	scriptName: 'HydrationMarker',
})
export class HydrationMarker extends Component {
	public label = 'marker';
}
